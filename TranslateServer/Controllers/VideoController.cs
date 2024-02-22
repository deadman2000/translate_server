using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [AuthAdmin]
    [Route("api/[controller]")]
    [ApiController]
    public class VideoController : ApiController
    {
        private readonly VideoStore _video;
        private readonly VideoTasksStore _videoTasks;

        public VideoController(VideoStore video, VideoTasksStore videoTasks)
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
        public async Task<ActionResult> Post([FromBody] Video video, [FromServices] ProjectsStore projects)
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

        [HttpPost("{id}/restart")]
        public async Task<ActionResult> Restart(string id)
        {
            var video = await _video.Get(v => v.Id == id);
            if (video == null)
                return NotFound();

            await _video.Update(v => v.Id == id)
                .Set(v => v.Completed, false)
                .Set(v => v.FramesProcessed, 0)
                .Execute();

            await _videoTasks.CreateGetText(video.Project, video.VideoId, video.Filters, video.FramesCount);

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id, [FromServices] VideoTextStore videoText, [FromServices] VideoReferenceStore references)
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
            return Ok(runners.List().Where(r => r != null).OrderBy(r => r.Id));
        }

        [HttpDelete("runner/{id}")]
        public ActionResult DeleteRunner(string id, [FromServices] RunnersService runners)
        {
            runners.Delete(id);
            return Ok();
        }
    }
}
