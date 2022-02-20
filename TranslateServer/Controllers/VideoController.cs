using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [AuthAdmin]
    [Route("api/[controller]")]
    [ApiController]
    public class VideoController : ApiController
    {
        private readonly VideoService _video;
        private readonly VideoTasksService _videoTasks;

        public VideoController(VideoService video, VideoTasksService videoTasks)
        {
            _video = video;
            _videoTasks = videoTasks;
        }

        [HttpGet]
        public async Task<ActionResult> Get()
        {
            var all = await _video.All();
            return Ok(all);
        }

        [HttpPost]
        public async Task<ActionResult> Post([FromBody] Video video, [FromServices] ProjectsService projects)
        {
            var pr = await projects.Get(p => p.Code == video.Project);
            if (pr == null)
                return ApiBadRequest("Project not found");

            var exists = await _video.Get(v => v.VideoId == video.VideoId);
            if (exists != null)
                return ApiBadRequest("Video exists");

            video.Completed = false;
            video.FramesCount = 0;
            video.FramesProcessed = 0;
            await _video.Insert(video);

            await _videoTasks.CreateGetInfo(video.Project, video.VideoId);

            return Ok(video);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id, [FromServices] VideoTextService videoText, [FromServices] VideoReferenceService references)
        {
            await Task.WhenAll(new Task[]
            {
                _video.DeleteOne(v => v.VideoId == id),
                _videoTasks.Delete(t => t.VideoId == id),
                videoText.Delete(t => t.VideoId == id),
                references.Delete(r => r.VideoId == id)
            });
            try
            {
                Directory.Delete($"resources/videos/{id}", true);
            }
            catch { }
            return Ok();
        }

        [HttpGet("runners")]
        public ActionResult Runners([FromServices] RunnersService runners)
        {
            return Ok(runners.List().Where(r => r != null));
        }

        [HttpDelete("runner/{id}")]
        public ActionResult DeleteRunner(string id, [FromServices] RunnersService runners)
        {
            runners.Delete(id);
            return Ok();
        }
    }
}
