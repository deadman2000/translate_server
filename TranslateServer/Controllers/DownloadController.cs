﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SCI_Lib;
using SharpCompress.Archives;
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
        public Task Full(string project)
        {
            return GenerateArchive(project, true);
        }

        [HttpGet("patch")]
        public Task Patch(string project)
        {
            return GenerateArchive(project, false);
        }

        private async Task GenerateArchive(string project, bool full)
        {
            var dir = _sci.Copy(project);
            var ms = new MemoryStream();
            try
            {
                HashSet<string> excludeSources = new();
                using var archive = new ZipArchive(ms, ZipArchiveMode.Create, true);

                //Применяем патчи
                var patches = (await _patches.Query(p => p.Project == project && !p.Deleted)).ToList();
                foreach (var p in patches)
                {
                    var patchPath = Path.Combine(dir, p.FileName);
                    if (System.IO.File.Exists(patchPath))
                        System.IO.File.Delete(patchPath);

                    using FileStream fs = new(patchPath, FileMode.CreateNew, FileAccess.Write);
                    await _patches.Download(p.FileId, fs);
                }

                // Читаем ресурсы
                var package = SCIPackage.Load(dir);
                var enc = package.GameEncoding;

                // Патчим строковые ресурсы и добавляем в архив
                {
                    var texts = await _translate.Query(t => t.Project == project && !t.Deleted && t.NextId == null);
                    foreach (var g in texts.GroupBy(t => t.Volume))
                    {
                        var resourceName = g.Key.Replace('_', '.');
                        var res = package.GetResource(resourceName);
                        var strings = res.GetStrings();
                        for (int i = 0; i < strings.Length; i++)
                            strings[i] = enc.EscapeString(strings[i]);

                        var trStrings = (string[])strings.Clone();

                        foreach (var t in g)
                            trStrings[t.Number] = t.Text;

                        if (trStrings.SequenceEqual(strings)) continue; // Skip not changed

                        for (int i = 0; i < trStrings.Length; i++)
                            trStrings[i] = enc.UnescapeString(trStrings[i]);

                        res.SetStrings(trStrings);
                        var bytes = res.GetPatch();
                        var entry = archive.CreateEntry(resourceName);
                        using var s = entry.Open();
                        res.Save(s, bytes);

                        excludeSources.Add(resourceName.ToLower());
                    }
                }

                // Добавляем в архив оставшиеся патчи
                foreach (var p in patches)
                {
                    if (excludeSources.Contains(p.FileName.ToLower())) continue;

                    archive.CreateEntryFromFile(Path.Combine(dir, p.FileName), p.FileName);
                    excludeSources.Add(p.FileName.ToLower());
                }

                // Добавляем в архив остальные ресурсы
                if (full)
                {
                    var path = package.GameDirectory;
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        var relativePath = Path.GetRelativePath(path, f);
                        if (excludeSources.Contains(relativePath.ToLower())) continue;

                        archive.CreateEntryFromFile(f, relativePath);
                    }
                }
            }
            finally
            {
                Directory.Delete(dir, true);
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
