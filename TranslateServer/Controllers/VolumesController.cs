using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
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
            return Ok(volumes.Select(v => new { v.Name }));
        }

        [HttpGet("{volume}")]
        public async Task<ActionResult> Get(string project, string volume)
        {
            var vol = await _volumes.Get(v => v.Project == project && v.Name == volume);
            return Ok(vol);
        }
    }
}
