using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Helpers;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ApiController
    {
        private readonly ProjectsService _project;
        private readonly ServerConfig _config;

        public ProjectsController(IOptions<ServerConfig> opConfig, ProjectsService project)
        {
            _project = project;
            _config = opConfig.Value;
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
        public async Task<ActionResult> Reindex(string shortName, [FromServices] SearchService elastic, [FromServices] TextsService texts, [FromServices] TranslateService translate, [FromServices] VolumesService volumes)
        {
            var textsList = await texts.Query(t => t.Project == shortName);
            var tr = await translate.Query(t => t.Project == shortName && t.NextId == null && !t.Deleted);

            bool changed = false;
            foreach (var txt in textsList)
            {
                var letters = txt.Letters;
                txt.RecalcLetters();
                if (txt.Letters != letters)
                {
                    changed = true;
                    await texts.Update(t => t.Id == txt.Id)
                        .Set(t => t.Letters, txt.Letters)
                        .Execute();
                }
            }

            if (changed)
            {
                await volumes.RecalcLetters(shortName, texts);
                await _project.RecalcLetters(shortName, volumes);
            }

            await elastic.DeleteProject(shortName);
            await elastic.InsertTexts(textsList);
            if (tr.Any())
                await elastic.InsertTranslates(tr.ToList());

            return Ok();
        }

        [AuthAdmin]
        [HttpDelete("{shortName}")]
        public async Task<ActionResult> Delete(string shortName, [FromServices] VolumesService volumes, [FromServices] TextsService texts)
        {
            await volumes.Delete(v => v.Project == shortName);
            await texts.Delete(v => v.Project == shortName);
            await _project.DeleteOne(p => p.Code == shortName);

            return Ok();
        }
    }
}
