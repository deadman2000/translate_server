using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SCI_Lib;
using SCI_Lib.Resources;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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

        public ImportController(TranslateService translateService, SearchService elastic, VolumesStore volumes, TextsStore texts, TranslateStore translate)
        {
            _translateService = translateService;
            _elastic = elastic;
            _volumes = volumes;
            _texts = texts;
            _translate = translate;
        }

        [RequestFormLimits(ValueLengthLimit = 500 * 1024 * 1024, MultipartBodyLengthLimit = 500 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost("{shortName}/json")]
        public async Task<ActionResult> Json(string shortName, [FromForm] IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var book = await JsonSerializer.DeserializeAsync<ImportBook>(ms);

            var importer = new Importer(shortName, _volumes, _elastic, _texts, _translate);
            await importer.Import(book);

            var allVolumes = await _volumes.Query(v => v.Project == shortName);
            foreach (var vol in allVolumes)
                await _translateService.UpdateVolumeProgress(shortName, vol.Code);
            await _translateService.UpdateProjectProgress(shortName);

            return Ok();
        }

        [HttpPost("{shortName}/package")]
        public async Task<ActionResult> Package(string shortName)
        {
            var package = SCIPackage.Load(@"D:\Dos\GAMES\PQ3_RUS\");
            var texts = package.GetResources<ResText>();
            foreach (var txt in texts)
            {
                var volume = Volume.FileNameToCode(txt.FileName);

                await _translate.Delete(t => t.Project == shortName && t.Volume == volume);

                var strings = txt.GetStrings();
                for (int i = 0; i < strings.Length; i++)
                {
                    await _translate.Insert(new TextTranslate()
                    {
                        Project = shortName,
                        Volume = volume,
                        Number = i,
                        Text = strings[i],
                        Author = "import",
                        Editor = "import",
                        DateCreate = DateTime.UtcNow,
                    });
                }

                await _texts.Update(t => t.Project == shortName && t.Volume == volume)
                    .Set(t => t.HasTranslate, true)
                    .ExecuteMany();

                await _translateService.UpdateVolumeProgress(shortName, volume);
            }

            await _translateService.UpdateProjectProgress(shortName);
            return Ok();
        }
    }
}
