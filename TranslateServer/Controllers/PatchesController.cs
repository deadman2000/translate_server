using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/[controller]")]
    [ApiController]
    public class PatchesController : ApiController
    {
        private readonly PatchesStore _patches;

        public PatchesController(PatchesStore patches)
        {
            _patches = patches;
        }

        [HttpGet]
        public async Task<ActionResult> Get(string project)
        {
            var patches = await _patches.Query(p => p.Project == project && !p.Deleted);
            return Ok(patches
                .OrderBy(p => p.Extension)
                .ThenBy(p => p.Number));
        }

        private async Task<Patch> SavePatch(string project, string fileName, Stream stream)
        {
            var patch = await _patches.Get(p => p.Project == project && p.FileName == fileName.ToLower() && !p.Deleted);
            if (patch == null)
                return await _patches.Save(project, fileName, stream, UserLogin);

            await _patches.Update(patch, fileName, stream, UserLogin);
            return patch;
        }

        [RequestFormLimits(ValueLengthLimit = 16 * 1024 * 1024, MultipartBodyLengthLimit = 16 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost]
        public async Task<ActionResult> Upload(string project, [FromForm] IFormFile file)
        {
            var patch = await SavePatch(project, file.FileName, file.OpenReadStream());
            return Ok(patch);
        }

        [RequestFormLimits(ValueLengthLimit = 16 * 1024 * 1024, MultipartBodyLengthLimit = 16 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost("zip")]
        public async Task<ActionResult> UploadZip(string project, [FromForm] IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            using var archive = new ZipArchive(ms);
            foreach (var ent in archive.Entries)
            {
                if (ent.Length > 0)
                    await SavePatch(project, ent.FullName, ent.Open());
            }

            return Ok();
        }

        [HttpGet("{id}")]
        public async Task Download(string project, string id)
        {
            var patch = await _patches.Get(p => p.Id == id && p.Project == project);
            if (patch == null)
            {
                Response.StatusCode = 404;
                return;
            }

            Response.StatusCode = 200;
            Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{patch.FileName}\"");
            Response.Headers.Add(HeaderNames.ContentType, "application/octet-stream");
            await _patches.Download(patch.FileId, Response.Body);
            await Response.Body.FlushAsync();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            await _patches.Delete(id);
            return Ok();
        }
    }
}
