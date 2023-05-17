using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
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

        public DownloadController(SCIService sci, TranslateStore translate, PatchesStore patches)
        {
            _sci = sci;
            _translate = translate;
            _patches = patches;
        }

        [HttpGet("full")]
        public Task Full(string project)
        {
            return GenerateArchive(project, true, true);
        }

        [HttpGet("patch")]
        public Task Patch(string project)
        {
            return GenerateArchive(project, false, true);
        }

        [HttpGet("texts")]
        public Task Texts(string project)
        {
            return GenerateArchive(project, false, false);
        }

        private async Task GenerateArchive(string project, bool full, bool withPatches)
        {
            var package = _sci.Load(project);

            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                HashSet<string> excludeSources = new();
                {
                    var texts = await _translate.Query(t => t.Project == project && !t.Deleted && t.NextId == null);
                    foreach (var g in texts.GroupBy(t => t.Volume))
                    {
                        var resourceName = g.Key.Replace('_', '.');
                        var res = package.GetResource(resourceName);
                        var strings = res.GetStrings();
                        var trStrings = (string[])strings.Clone();

                        foreach (var t in g)
                            trStrings[t.Number] = t.Text;

                        if (trStrings.SequenceEqual(strings)) continue; // Skip not changed

                        res.SetStrings(trStrings);
                        var bytes = res.GetPatch();
                        var entry = archive.CreateEntry(resourceName);
                        using var s = entry.Open();
                        res.Save(s, bytes);

                        excludeSources.Add(resourceName.ToLower());
                    }
                }

                if (withPatches)
                {
                    var patches = await _patches.Query(p => p.Project == project && !p.Deleted);
                    foreach (var p in patches)
                    {
                        var entry = archive.CreateEntry(p.FileName);
                        using var s = entry.Open();
                        await _patches.Download(p.FileId, s);

                        excludeSources.Add(p.FileName.ToLower());
                    }
                }

                if (full)
                {
                    var path = _sci.GetProjectPath(project);
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        var relativePath = Path.GetRelativePath(path, f);
                        if (excludeSources.Contains(relativePath.ToLower())) continue;

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
    }
}
