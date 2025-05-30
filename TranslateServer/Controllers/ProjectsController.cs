﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SCI_Lib;
using SCI_Lib.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Helpers;
using TranslateServer.Model.Import;
using TranslateServer.Requests;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ApiController
    {
        private readonly ILogger<ProjectsController> _logger;
        private readonly TextsStore _texts;
        private readonly VolumesStore _volumes;
        private readonly TranslateStore _translates;
        private readonly TranslateService _translateService;
        private readonly SearchService _elastic;
        private readonly SCIService _sci;
        private readonly PatchesStore _patches;

        public ProjectsController(
            ILogger<ProjectsController> logger,
            ProjectsStore project,
            TextsStore texts,
            VolumesStore volumes,
            TranslateStore translates,
            TranslateService translateService,
            SearchService elastic,
            SCIService sci,
            PatchesStore patches
        )
        {
            _logger = logger;
            _projects = project;
            _texts = texts;
            _volumes = volumes;
            _translates = translates;
            _translateService = translateService;
            _elastic = elastic;
            _sci = sci;
            _patches = patches;
        }

        [HttpGet]
        public async Task<ActionResult> GetList()
        {
            if (IsSharedUser)
                return Ok(await _projects.Shared());

            return Ok(await _projects.All());
        }

        public class CreateProjectRequest
        {
            public string Name { get; set; }

            public string Code { get; set; }

            public string Engine { get; set; }
        }

        [AuthAdmin]
        [HttpPost("create")]
        public async Task<ActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            var project = new Project
            {
                Name = request.Name,
                Code = request.Code,
                Engine = request.Engine ?? "sci",
            };

            await _projects.Insert(project);

            return Ok(project);
        }

        [HttpGet("{project}")]
        public async Task<ActionResult> GetProject(string project)
        {
            if (!await HasAccessToProject(project)) return NotFound();

            var proj = await _projects.GetProject(project);
            if (proj == null) return NotFound();
            return Ok(proj);
        }

        [AuthAdmin]
        [RequestFormLimits(ValueLengthLimit = 500 * 1024 * 1024, MultipartBodyLengthLimit = 500 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost("{project}/upload")]
        public async Task<ActionResult> Upload(string project, [FromForm] IFormFile file)
        {
            string targetDir = _sci.GetProjectPath(project);
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);

            try
            {
                await ExtractToDir(file, targetDir);
                RenameFilesToUpper(targetDir);

                await _projects.Update(p => p.Code == project && p.Status == ProjectStatus.New)
                    .Set(p => p.Status, ProjectStatus.TextExtract)
                    .Execute();
            }
            catch (InvalidDataException)
            {
                return ApiBadRequest("Wrong zip archive");
            }

            return Ok();
        }

        [AuthAdmin]
        [HttpPost("{project}/reindex")]
        public async Task<ActionResult> Reindex(string project)
        {
            _logger.LogInformation($"Reindex {project}");
            var textsList = await _texts.Query(t => t.Project == project);
            var tr = await _translates.Query(t => t.Project == project && t.NextId == null && !t.Deleted);

            await CheckTranslateFlag(textsList, tr);

            bool changed = false;
            foreach (var txt in textsList)
            {
                var letters = txt.Letters;
                txt.RecalcLetters();
                if (txt.Letters != letters)
                {
                    changed = true;
                    await _texts.Update(t => t.Id == txt.Id)
                        .Set(t => t.Letters, txt.Letters)
                        .Execute();
                }
            }

            if (changed)
            {
                await _volumes.RecalcLetters(project, _texts);
                await _projects.RecalcLetters(project, _volumes);
            }

            var volumes = await _volumes.Query(v => v.Project == project);
            foreach (var vol in volumes)
            {
                await _translateService.UpdateVolumeTotal(project, vol.Code);
                await _translateService.UpdateVolumeProgress(project, vol.Code);
            }
            await _translateService.UpdateProjectTotal(project);
            await _translateService.UpdateProjectProgress(project);

            await _elastic.DeleteProject(project);
            await _elastic.InsertTexts(textsList);
            if (tr.Any())
                await _elastic.InsertTranslates(tr.ToList());

            _logger.LogInformation($"Reindex end {project}");
            return Ok();
        }

        private async Task CheckTranslateFlag(List<TextResource> texts, List<TextTranslate> translates)
        {
            var dict = texts.ToDictionary(t => t.Volume + ":" + t.Number);
            foreach (var tr in translates)
            {
                if (dict.TryGetValue(tr.Volume + ":" + tr.Number, out var text))
                {
                    var approved = text.TranslateApproved || tr.Text == text.Text;

                    if (!text.HasTranslate || text.TranslateApproved != approved)
                    {
                        await _texts.Update(t => t.Id == text.Id)
                            .Set(t => t.HasTranslate, true)
                            .Set(t => t.TranslateApproved, approved)
                            .Execute();
                    }
                }
            }
        }

        [AuthAdmin]
        [HttpDelete("{project}")]
        public async Task<ActionResult> Delete(string project)
        {
            await _volumes.Delete(v => v.Project == project);
            await _texts.Delete(v => v.Project == project);
            await _projects.DeleteOne(p => p.Code == project);
            await _translates.Delete(t => t.Project == project);
            _sci.DeletePackage(project);
            foreach (var p in await _patches.Query(p => p.Project == project))
                await _patches.FullDelete(p.Id);

            await _elastic.DeleteProject(project);

            return Ok();
        }

        [AuthAdmin]
        [HttpGet("{project}/byuser/{user}")]
        public async Task<ActionResult> ByUser(string project, string user)
        {
            // Translates
            var trList = await _translates.Query()
                .Where(t => t.Project == project && t.Editor == user && t.NextId == null && !t.Deleted)
                .SortAsc(t => t.Number)
                .Execute();

            // Texts
            List<dynamic> list = new();
            foreach (var tr in trList)
            {
                var text = await _texts.Get(t => t.Project == project && t.Volume == tr.Volume && t.Number == tr.Number);
                if (text != null) list.Add(new
                {
                    text,
                    tr = new TranslateInfo(tr)
                });
            }

            return Ok(list);
        }

        [AuthAdmin]
        [RequestFormLimits(ValueLengthLimit = 500 * 1024 * 1024, MultipartBodyLengthLimit = 500 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost("{project}/json")]
        public async Task<ActionResult> Json(string project, [FromForm] IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            var book = await JsonSerializer.DeserializeAsync<ImportBook>(ms);

            var importer = new Importer(project, _volumes, _elastic, _texts, _translates);
            await importer.Import(book);

            var allVolumes = await _volumes.Query(v => v.Project == project);
            foreach (var vol in allVolumes)
                await _translateService.UpdateVolumeProgress(project, vol.Code);
            await _translateService.UpdateProjectProgress(project);

            return Ok();
        }

        [AuthAdmin]
        [RequestFormLimits(ValueLengthLimit = 500 * 1024 * 1024, MultipartBodyLengthLimit = 500 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost("{project}/import")]
        public async Task<ActionResult> Import(string project, [FromForm] IFormFile file)
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                await ExtractToDir(file, dir);
                RenameFilesToUpper(dir);

                var package = SCIPackage.Load(dir);
                var enc = package.GameEncoding;

                var resources = package.GetTextResources();
                await _translates.Delete(t => t.Project == project);
                foreach (var res in resources)
                {
                    Console.WriteLine(res.FileName);
                    var volume = Volume.FileNameToCode(res.FileName);

                    var strings = res.GetStrings();
                    for (int i = 0; i < strings.Length; i++)
                    {
                        var text = await _texts.Get(t => t.Project == project && t.Volume == volume && t.Number == i);
                        if (text == null) continue;

                        await _translates.Insert(new TextTranslate()
                        {
                            Project = project,
                            Volume = volume,
                            Number = i,
                            Text = enc.EscapeString(strings[i]),
                            Author = "import",
                            Editor = "import",
                            DateCreate = DateTime.UtcNow,
                        });

                    }

                    await _texts.Update(t => t.Project == project && t.Volume == volume)
                        .Set(t => t.HasTranslate, true)
                        .ExecuteMany();
                }

                _logger.LogInformation("Import patches");
                foreach (var p in await _patches.Query(p => p.Project == project))
                    await _patches.FullDelete(p.Id);

                var srcPack = await _sci.Load(project);
                var otherRes = package.GetResources(ResType.Font)
                    .Union(package.GetResources(ResType.View))
                    .Union(package.GetResources(ResType.Picture))
                    .Union(package.GetResources(ResType.Cursor));

                foreach (var res in otherRes)
                {
                    try
                    {
                        var cont = res.GetContent();
                        var src = srcPack.GetResource(res.Type, res.Number);
                        var srcCont = src.GetContent();
                        if (!Enumerable.SequenceEqual(cont, srcCont))
                            await _patches.Save(project, cont, res.FileName, "import");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Extract res {res.FileName} failed");
                    }
                }

                _logger.LogInformation("Import completed");
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
            //if (mapEntry == null) throw new Exception("RESOURCE.MAP file not found");

            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);

            Directory.CreateDirectory(targetDir);

            if (mapEntry != null && mapEntry.FullName.Length != mapEntry.Name.Length)
            {
                var dir = mapEntry.FullName.Substring(0, mapEntry.FullName.Length - mapEntry.Name.Length);
                archive.ExtractSubDir(targetDir, dir);
            }
            else
            {
                archive.ExtractToDirectory(targetDir);
            }
        }

        private static void RenameFilesToUpper(string dir)
        {
            var files = Directory.GetFiles(dir);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (fileName != fileName.ToUpper())
                {
                    System.IO.File.Move(file, Path.Combine(dir, fileName.ToUpper()));
                }
            }
        }

        public class ShareRequest
        {
            public bool Share { get; set; }
        }

        [AuthAdmin]
        [HttpPost("{project}/share")]
        public async Task<ActionResult> Share(string project, ShareRequest request)
        {
            await _projects.Update(p => p.Code == project)
                .Set(p => p.Shared, request.Share)
                .Execute();
            return Ok();
        }

        static bool HasWrongSymbol(string text, Encoding enc, out string newText)
        {
            var bytes = enc.GetBytes(text);
            newText = enc.GetString(bytes);
            return newText != text;
        }

        static int LineCount(string text)
        {
            return text.Count(c => c == '\n');
        }

        [HttpGet("{project}/validate")]
        public async Task<ActionResult> Validate(string project)
        {
            var texts = await _texts.Query(t => t.Project == project);
            var textsDict = texts.ToDictionary(t => t.GetCode(), t => t);
            var translates = await _translates.Query(t => t.Project == project && t.NextId == null && !t.Deleted);
            var enc = Encoding.GetEncoding(866);

            // Symbols check
            List<dynamic> symbols = new();
            foreach (var t in translates)
            {
                if (HasWrongSymbol(t.Text, enc, out string converted))
                    symbols.Add(new
                    {

                        t.Volume,
                        t.Number,
                        tr = t.Text,
                        converted
                    });
            }


            // Line count check
            List<dynamic> lines = new();
            foreach (var t in translates)
            {
                var src = textsDict[t.GetCode()];

                if (LineCount(t.Text) != LineCount(src.Text))
                    lines.Add(new
                    {

                        t.Volume,
                        t.Number,
                        tr = BaseEscaper.Slash.Escape(t.Text),
                        src = BaseEscaper.Slash.Escape(src.Text)
                    });
            }

            return Ok(new
            {
                symbols,
                lines
            });
        }

        public class UpdateRequest
        {
            public string Name { get; set; }
            public string CodePage { get; set; }
        }

        [AuthAdmin]
        [HttpPost("{project}/update")]
        public async Task<ActionResult> Update(string project, UpdateRequest request)
        {
            var upd = _projects.Update(p => p.Code == project);
            if (!string.IsNullOrEmpty(request.Name))
                upd.Set(p => p.Name, request.Name);
            if (!string.IsNullOrEmpty(request.CodePage))
                upd.Set(p => p.CodePage, request.CodePage);
            await upd.Execute();

            return Ok();
        }
    }
}