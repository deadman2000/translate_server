using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectsController : ApiController
    {
        private readonly ProjectService _project;

        public ProjectsController(ProjectService project)
        {
            _project = project;
        }

        [HttpGet]
        public async Task<ActionResult> GetList()
        {
            var list = await _project.GetProjects();
            return Ok(list);
        }

        public class CreateProjectRequest
        {
            public string Name { get; set; }

            public string ShortName { get; set; }
        }

        [HttpPost("create")]
        public async Task<ActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            var project = new Project
            {
                Name = request.Name,
                ShortName = request.ShortName,
            };

            await _project.Create(project);

            return Ok(project);
        }

        [HttpGet("{shortName}")]
        public async Task<ActionResult> GetProject(string shortName)
        {
            var project = await _project.GetProject(shortName);
            return Ok(project);
        }

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

                if (mapEntry.FullName.Length != mapEntry.Name.Length)
                {
                    var dir = mapEntry.FullName.Substring(0, mapEntry.FullName.Length - mapEntry.Name.Length);
                    var dirEntry = archive.Entries.FirstOrDefault(d => d.FullName == dir);
                    if (dirEntry != null)
                    {
                        dirEntry.ExtractToFile("game");
                    }
                    Console.WriteLine(dir);
                }

                /*foreach (var e in archive.Entries)
                {
                    Console.WriteLine(e.FullName);
                }*/
            }
            catch (InvalidDataException)
            {
                return ApiBadRequest("Wrong zip archive");
            }

            return Ok();
        }
    }

}
