﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Helpers;
using TranslateServer.Model;
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
        private readonly ProjectsStore _project;
        private readonly ServerConfig _config;
        private readonly TextsStore _texts;
        private readonly VolumesStore _volumes;
        private readonly TranslateStore _translates;

        public ProjectsController(IOptions<ServerConfig> opConfig, ProjectsStore project, TextsStore texts, VolumesStore volumes, TranslateStore translates)
        {
            _project = project;
            _config = opConfig.Value;
            _texts = texts;
            _volumes = volumes;
            _translates = translates;
        }

        [HttpGet]
        public async Task<ActionResult> GetList()
        {
            var list = await _project.All();
            return Ok(list);
        }

        public class CreateProjectRequest
        {
            public string Name { get; set; }

            public string Code { get; set; }
        }

        [AuthAdmin]
        [HttpPost("create")]
        public async Task<ActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            var project = new Project
            {
                Name = request.Name,
                Code = request.Code,
            };

            await _project.Insert(project);

            return Ok(project);
        }

        [HttpGet("{shortName}")]
        public async Task<ActionResult> GetProject(string shortName)
        {
            var project = await _project.GetProject(shortName);
            return Ok(project);
        }

        [AuthAdmin]
        [RequestFormLimits(ValueLengthLimit = 500 * 1024 * 1024, MultipartBodyLengthLimit = 500 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost("{shortName}/upload")]
        public async Task<ActionResult> Upload(string shortName, [FromForm] IFormFile file)
        {
            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                using var archive = new ZipArchive(ms);

                var mapEntry = archive.Entries.FirstOrDefault(e => e.Name.Equals("RESOURCE.MAP", StringComparison.OrdinalIgnoreCase));
                if (mapEntry == null)
                    return ApiBadRequest("RESOURCE.MAP file not found");

                string targetDir = Path.GetFullPath($"{_config.ProjectsDir}/{shortName}/");
                Console.WriteLine(targetDir);
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

                await _project.Update(shortName).Set(p => p.Status, ProjectStatus.Processing).Execute();
            }
            catch (InvalidDataException)
            {
                return ApiBadRequest("Wrong zip archive");
            }

            return Ok();
        }

        [AuthAdmin]
        [HttpPost("{shortName}/reindex")]
        public async Task<ActionResult> Reindex(string shortName, [FromServices] SearchService elastic, [FromServices] TranslateService translateService)
        {
            var textsList = await _texts.Query(t => t.Project == shortName);
            var tr = await _translates.Query(t => t.Project == shortName && t.NextId == null && !t.Deleted);

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
                await _volumes.RecalcLetters(shortName, _texts);
                await _project.RecalcLetters(shortName, _volumes);
            }

            var volumes = await _volumes.Query(v => v.Project == shortName);
            foreach (var vol in volumes)
            {
                await translateService.UpdateVolumeTotal(shortName, vol.Code);
                await translateService.UpdateVolumeProgress(shortName, vol.Code);
            }
            await translateService.UpdateProjectTotal(shortName);
            await translateService.UpdateProjectProgress(shortName);

            await elastic.DeleteProject(shortName);
            await elastic.InsertTexts(textsList);
            if (tr.Any())
                await elastic.InsertTranslates(tr.ToList());

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
        [HttpDelete("{shortName}")]
        public async Task<ActionResult> Delete(string shortName, [FromServices] SCIService sci, [FromServices] PatchesStore patches)
        {
            await _volumes.Delete(v => v.Project == shortName);
            await _texts.Delete(v => v.Project == shortName);
            await _project.DeleteOne(p => p.Code == shortName);
            await _translates.Delete(t => t.Project == shortName);
            sci.DeletePackage(shortName);
            foreach (var p in await patches.Query(p => p.Project == shortName))
                await patches.FullDelete(p.Id);

            return Ok();
        }

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
    }
}
