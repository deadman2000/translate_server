using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/[controller]")]
    [ApiController]
    public class VolumesController : ApiController
    {
        private readonly VolumesStore _volumes;

        public VolumesController(VolumesStore volumes)
        {
            _volumes = volumes;
        }

        [HttpGet]
        public async Task<ActionResult> Get(string project)
        {
            var volumes = await _volumes.Query(v => v.Project == project);
            return Ok(volumes);
        }

        [HttpGet("{volume}")]
        public async Task<ActionResult> Get(string project, string volume)
        {
            var vol = await _volumes.Get(v => v.Project == project && v.Code == volume);
            if (vol == null)
                return NotFound();

            return Ok(vol);
        }

        public class UpdateRequest
        {
            public string Description { get; set; }
        }

        [HttpPost("{volume}")]
        public async Task<ActionResult> Update(string project, string volume, UpdateRequest request)
        {
            await _volumes.Update(v => v.Project == project && v.Code == volume)
                .Set(v => v.Description, request.Description)
                .Execute();

            return Ok();
        }
    }
}
