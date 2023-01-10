using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SCI_Lib;
using SCI_Lib.Resources;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TranslateServer.Helpers;
using TranslateServer.Model;
using TranslateServer.Model.Import;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [AuthAdmin]
    [Route("api/[controller]")]
    [ApiController]
    public class ImportController : ApiController
    {
        private readonly TranslateService _translateService;
        private readonly SearchService _elastic;
        private readonly VolumesStore _volumes;
        private readonly TextsStore _texts;
        private readonly TranslateStore _translate;
        private readonly SCIService _sci;
        private readonly PatchesStore _patches;

        public ImportController(TranslateService translateService,
            SearchService elastic,
            VolumesStore volumes,
            TextsStore texts,
            TranslateStore translate,
            SCIService sci,
            PatchesStore patches)
        {
            _translateService = translateService;
            _elastic = elastic;
            _volumes = volumes;
            _texts = texts;
            _translate = translate;
            _sci = sci;
            _patches = patches;
        }

        [RequestFormLimits(ValueLengthLimit = 500 * 1024 * 1024, MultipartBodyLengthLimit = 500 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost("{project}/json")]
        public async Task<ActionResult> Json(string project, [FromForm] IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var book = await JsonSerializer.DeserializeAsync<ImportBook>(ms);

            var importer = new Importer(project, _volumes, _elastic, _texts, _translate);
            await importer.Import(book);

            var allVolumes = await _volumes.Query(v => v.Project == project);
            foreach (var vol in allVolumes)
                await _translateService.UpdateVolumeProgress(project, vol.Code);
            await _translateService.UpdateProjectProgress(project);

            return Ok();
        }

        [HttpPost("{project}/package")]
        public async Task<ActionResult> Package(string project, [FromForm] IFormFile file)
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                await ExtractToDir(file, dir);

                var package = SCIPackage.Load(dir);

                var resources = package.GetTextResources();
                await _translate.Delete(t => t.Project == project);
                foreach (var res in resources)
                {
                    Console.WriteLine(res.FileName);
                    var volume = Volume.FileNameToCode(res.FileName);

                    var strings = res.GetStrings();
                    for (int i = 0; i < strings.Length; i++)
                    {
                        var text = await _texts.Get(t => t.Project == project && t.Volume == volume && t.Number == i);
                        if (text == null) continue;

                        await _translate.Insert(new TextTranslate()
                        {
                            Project = project,
                            Volume = volume,
                            Number = i,
                            Text = strings[i],
                            Author = "import",
                            Editor = "import",
                            DateCreate = DateTime.UtcNow,
                        });

                    }

                    await _texts.Update(t => t.Project == project && t.Volume == volume)
                        .Set(t => t.HasTranslate, true)
                        .ExecuteMany();
                }

                var patches = await _patches.Query(p => p.Project == project);
                foreach (var patch in patches)
                    await _patches.FullDelete(patch.Id);

                var srcPack = _sci.Load(project);
                var otherRes = package.GetResources(ResType.Font)
                    .Union(package.GetResources(ResType.View))
                    .Union(package.GetResources(ResType.Picture))
                    .Union(package.GetResources(ResType.Cursor));

                foreach (var res in otherRes)
                {
                    var cont = res.GetContent();
                    var src = srcPack.GetResource(res.Type, res.Number);
                    var srcCont = src.GetContent();
                    if (!Enumerable.SequenceEqual(cont, srcCont))
                        await _patches.Save(project, cont, res.FileName, "import");
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }

            return Ok();
        }

        private static async Task ExtractToDir(IFormFile file, string targetDir)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            using var archive = new ZipArchive(ms);

            var mapEntry = archive.Entries.FirstOrDefault(e => e.Name.Equals("RESOURCE.MAP", StringComparison.OrdinalIgnoreCase));
            if (mapEntry == null)
                throw new Exception("RESOURCE.MAP file not found");

            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);

            Directory.CreateDirectory(targetDir);

            if (mapEntry.FullName.Length != mapEntry.Name.Length)
            {
                var dir = mapEntry.FullName.Substring(0, mapEntry.FullName.Length - mapEntry.Name.Length);
                archive.ExtractSubDir(targetDir, dir);
            }
            else
            {
                archive.ExtractToDirectory(targetDir);
            }

        }
    }
}
