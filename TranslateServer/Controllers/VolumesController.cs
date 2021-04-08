using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/[controller]")]
    [ApiController]
    public class VolumesController : ApiController
    {
        private readonly VolumesService _volumes;

        public VolumesController(VolumesService volumes)
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
            return Ok(vol);
        }
    }
}
