using Microsoft.AspNetCore.Mvc;
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

            await _videoTasks.GetInfo(video.VideoId, video.Project);

            return Ok(video);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            await _video.DeleteOne(v => v.VideoId == id);
            await _videoTasks.Delete(t => t.VideoId == id);
            // TODO Clean up text resources
            return Ok();
        }

        [HttpGet("runners")]
        public ActionResult Runners([FromServices] RunnersService runners)
        {
            return Ok(runners.List());
        }
    }
}
