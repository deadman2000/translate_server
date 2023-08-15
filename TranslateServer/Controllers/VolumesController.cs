using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
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

        public VolumesController(VolumesStore volumes, ProjectsStore projects)
        {
            _volumes = volumes;
            _projects = projects;
        }

        [HttpGet]
        public async Task<ActionResult> Get(string project)
        {
            if (!await HasAccessToProject(project)) return NotFound();

            var volumes = await _volumes.Query(v => v.Project == project);
            if (!volumes.Any()) return NotFound();

            return Ok(volumes);
        }

        [HttpGet("{volume}")]
        public async Task<ActionResult> Get(string project, string volume)
        {
            if (!await HasAccessToProject(project)) return NotFound();
       
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
            if (!await HasAccessToProject(project)) return NotFound();
      
            await _volumes.Update(v => v.Project == project && v.Code == volume)
                .Set(v => v.Description, request.Description)
                .Execute();

            return Ok();
        }
    }
}
