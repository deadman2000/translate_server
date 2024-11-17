using AGSUnpacker.Lib.Translation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SCI_Lib.Resources;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Route("api/projects/{project}/[controller]")]
    [ApiController]
    public class DownloadController : ApiController
    {
        private readonly SCIService _sci;
        private readonly TranslateStore _translate;
        private readonly PatchesStore _patches;
        private readonly VolumesStore _volumes;
        private readonly WordsStore _words;
        private readonly SuffixesStore _suffixes;
        private readonly SaidStore _saids;
        private readonly SynonymStore _synonyms;

        public DownloadController(SCIService sci,
            TranslateStore translate,
            PatchesStore patches,
            ProjectsStore projects,
            VolumesStore volumes,
            WordsStore words,
            SuffixesStore suffixes,
            SaidStore saids,
            SynonymStore synonyms
        )
        {
            _sci = sci;
            _translate = translate;
            _patches = patches;
            _projects = projects;
            _volumes = volumes;
            _words = words;
            _suffixes = suffixes;
            _saids = saids;
            _synonyms = synonyms;
        }

        [HttpGet("source")]
        public async Task Source(string project)
        {
            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var path = _sci.GetProjectPath(project);
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    var relativePath = Path.GetRelativePath(path, f);
                    archive.CreateEntryFromFile(f, relativePath);
                }
            }

            ms.Seek(0, SeekOrigin.Begin);
            Response.StatusCode = 200;
            Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{project}_src.zip\"");
            Response.Headers.Add(HeaderNames.ContentType, "application/octet-stream");
            await ms.CopyToAsync(Response.Body);
        }

        [HttpGet("full")]
        public async Task Full(string project)
        {
            var proj = await _projects.GetProject(project);
            await GenerateSCIZip(proj, true);
        }

        [HttpGet("patch")]
        public async Task Patch(string project)
        {
            var proj = await _projects.GetProject(project);

            if (proj.Engine == "ags")
                await GenerateAGSZip(proj, false);
            else
                await GenerateSCIZip(proj, false);
        }

        private async Task GenerateAGSZip(Project project, bool full)
        {
            var path = _sci.GetProjectPath(project.Code);

            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var volumes = await _volumes.Query(v => v.Project == project.Code);

                foreach (var vol in volumes)
                {
                    if (vol.Name.ToLower().EndsWith(".trs"))
                    {
                        var trsPath = Path.Combine(path, vol.Name);
                        AGSTranslation translation = AGSTranslation.ReadSourceFile(trsPath);

                        var texts = await _translate.Query(t => t.Project == project.Code && t.Volume == vol.Code && !t.Deleted && t.NextId == null);
                        foreach (var t in texts)
                            translation.TranslatedLines[t.Number] = t.Text.Replace("\n", "[");

                        string traPath;
                        if (volumes.Count > 1)
                            traPath = Path.GetFileNameWithoutExtension(vol.Name) + ".tra";
                        else
                            traPath = "Russian.tra";
                        var entry = archive.CreateEntry(traPath);

                        var msTRA = new MemoryStream();
                        translation.TranslateEncoding = project.GetEncoding();
                        translation.Compile(msTRA);
                        msTRA.Seek(0, SeekOrigin.Begin);

                        using var s = entry.Open();
                        msTRA.CopyTo(s);
                    }
                    else
                    {
                        var traPath = Path.Combine(path, vol.Name, "English.tra");
                        if (Path.Exists(traPath))
                        {
                            AGSTranslation translation = new();
                            translation.Decompile(traPath);

                            var texts = await _translate.Query(t => t.Project == project.Code && t.Volume == vol.Code && !t.Deleted && t.NextId == null);
                            foreach (var t in texts)
                                translation.TranslatedLines[t.Number] = t.Text.Replace("\n", "[");

                            var entry = archive.CreateEntry($"{vol.Name}/Russian.tra");

                            var msTRA = new MemoryStream();
                            translation.TranslateEncoding = project.GetEncoding();
                            translation.Compile(msTRA);
                            msTRA.Seek(0, SeekOrigin.Begin);

                            using var s = entry.Open();
                            msTRA.CopyTo(s);
                        }
                    }
                }

                var patches = (await _patches.Query(p => p.Project == project.Code && !p.Deleted)).ToList();
                foreach (var p in patches)
                {
                    var entry = archive.CreateEntry(p.FileName);
                    using var s = entry.Open();
                    await _patches.Download(p.FileId, s);
                }
            }

            string fileName = project.Code;
            if (!full) fileName += "_patch";

            ms.Seek(0, SeekOrigin.Begin);
            Response.StatusCode = 200;
            Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{fileName}.zip\"");
            Response.Headers.Add(HeaderNames.ContentType, "application/octet-stream");
            await ms.CopyToAsync(Response.Body);
        }


        private async Task GenerateSCIZip(Project proj, bool full)
        {
            var project = proj.Code;
            var package = _sci.Load(proj);

            Dictionary<string, byte[]> additionalFiles = new(); // Файлы, которые не являются ресурсами
            List<Resource> pathedRes = new();

            //Применяем патчи
            var patches = (await _patches.Query(p => p.Project == project && !p.Deleted)).ToList();
            foreach (var p in patches)
            {
                var data = await _patches.GetContent(p.FileId);
                if (IsPatchTranslate(p.FileName))
                {
                    var res = package.SetPatch(p.FileName, data);
                    pathedRes.Add(res);
                }
                else
                {
                    additionalFiles.Add(p.FileName, data);
                }
            }

            if (proj.HasSaid)
            {
                pathedRes.AddRange(await _words.Apply(package, project));
                pathedRes.Add(await _suffixes.Apply(package, project));
                pathedRes.AddRange(await _saids.Apply(package, project));
                pathedRes.AddRange(await _synonyms.Apply(package, project));
            }

            pathedRes.AddRange(await _translate.Apply(package, project));

            var ms = new MemoryStream();

            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                HashSet<string> addedFiles = new();

                // Добавляем в архив дополнительные патчи
                foreach (var kv in additionalFiles)
                {
                    var file = kv.Key;
                    if (addedFiles.Contains(file.ToLower())) continue;

                    var entry = archive.CreateEntry(file);
                    using var s = entry.Open();
                    s.Write(kv.Value);

                    addedFiles.Add(file.ToLower());
                }

                // Добавляем в архив пропатченные ресурсы
                foreach (var res in pathedRes)
                {
                    if (addedFiles.Contains(res.FileName.ToLower())) continue;

                    var entry = archive.CreateEntry(res.FileName);
                    using var s = entry.Open();
                    var bytes = res.GetPatch();
                    res.Save(s, bytes);

                    addedFiles.Add(res.FileName.ToLower());
                }

                // Добавляем в архив остальные ресурсы
                if (full)
                {
                    var path = package.GameDirectory;
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        var relativePath = Path.GetRelativePath(path, f);
                        if (addedFiles.Contains(relativePath.ToLower())) continue;

                        archive.CreateEntryFromFile(f, relativePath);
                    }
                }
            }

            string fileName = project;
            if (!full) fileName += "_patch";

            ms.Seek(0, SeekOrigin.Begin);
            Response.StatusCode = 200;
            Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{fileName}.zip\"");
            Response.Headers.Add(HeaderNames.ContentType, "application/octet-stream");
            await ms.CopyToAsync(Response.Body);
        }

        /// <summary>
        /// Возвращает true, если файл надо применить до перевода (например скрипт или текст)
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private static bool IsPatchTranslate(string fileName) => Path.GetExtension(fileName).ToLower() switch
        {
            ".scr" or ".tex" => true,
            _ => Path.GetFileNameWithoutExtension(fileName).ToLower() switch
            {
                "script" or "text" => true,
                _ => false,
            },
        };
    }
}
