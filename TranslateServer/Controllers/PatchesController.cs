using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/[controller]")]
    [ApiController]
    public class PatchesController : ApiController
    {
        private readonly PatchesService _patches;

        public PatchesController(PatchesService patches)
        {
            _patches = patches;
        }

        [HttpGet]
        public async Task<ActionResult> Get(string project)
        {
            var patches = await _patches.Query(v => v.Project == project);
            return Ok(patches);
        }

        [RequestFormLimits(ValueLengthLimit = 16 * 1024 * 1024, MultipartBodyLengthLimit = 16 * 1024 * 1024)]
        [DisableRequestSizeLimit]
        [HttpPost]
        public async Task<ActionResult> Upload(string project, [FromForm] IFormFile file)
        {
            var patch = await _patches.Save(project, file);
            return Ok(patch);
        }
    }
}
