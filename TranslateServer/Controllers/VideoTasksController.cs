using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoTasksController : ApiController
    {
        private readonly VideoTasksService _tasks;
        private readonly RunnersService _runners;
        private readonly VideoService _videos;

        public VideoTasksController(VideoTasksService tasks, RunnersService runners, VideoService videos)
        {
            _tasks = tasks;
            _runners = runners;
            _videos = videos;
        }

        [HttpGet]
        public async Task<ActionResult> GetTask(string runner)
        {
            if (runner == null) return BadRequest();

            _runners.RegisterActivity(runner, Request);
            var task = await _tasks.GetNext(runner);

            return Ok(task);
        }


        [HttpPost("ping")]
        public ActionResult Ping(string runner)
        {
            _runners.RegisterActivity(runner, Request);
            return Ok();
        }

        public class InfoRequest
        {
            public string TaskId { get; set; }
            public string Runner { get; set; }
            public int Frames { get; set; }
            public double Fps { get; set; }
        }

        [HttpPost("info")]
        public async Task<ActionResult> Info([FromBody] InfoRequest request)
        {
            _runners.RegisterActivity(request.Runner, Request);

            var task = await _tasks.Get(t => t.Id == request.TaskId && !t.Completed);
            if (task == null) return Ok();

            var result = await _tasks.Complete(request.TaskId, request.Runner);
            if (result.ModifiedCount > 0)
            {
                await _videos.Update(v => v.VideoId == task.VideoId)
                    .Set(v => v.FramesCount, request.Frames)
                    .Set(v => v.Fps, request.Fps)
                    .Execute();

                await _tasks.CreateGetFrames(task.Project, task.VideoId, request.Frames);

            }

            return Ok();
        }

        public class TextsRequest
        {
            public string TaskId { get; set; }
            public string Runner { get; set; }
            public IEnumerable<FrameTexts> Texts { get; set; }
        }

        public class FrameTexts
        {
            public int Frame { get; set; }
            public string Text { get; set; }
            public int T { get; set; }
        }

        [HttpPost("texts")]
        public async Task<ActionResult> Texts([FromBody] TextsRequest request, [FromServices] VideoTextService videoText, [FromServices] VideoService video)
        {
            _runners.RegisterActivity(request.Runner, Request);
      
            var task = await _tasks.Get(t => t.Id == request.TaskId);
            if (task == null) return Ok();

            var docs = request.Texts.Select(t => new VideoText
            {
                Project = task.Project,
                VideoId = task.VideoId,
                Frame = t.Frame,
                Text = t.Text,
                T = t.T
            });
            await videoText.Insert(docs);

            var result = await _tasks.Complete(request.TaskId, request.Runner);
            if (result.ModifiedCount > 0)
            {
                var sum = _tasks.Collection.AsQueryable()
                    .Where(t => t.Completed && t.VideoId == task.VideoId && t.Type == VideoTask.GET_TEXT)
                    .Sum(t => t.Count * t.FrameSkip);

                if (sum.HasValue)
                {
                    await video.Update()
                        .Where(v => v.VideoId == task.VideoId)
                        .Set(v => v.FramesProcessed, sum.Value)
                        .Execute();
                }
            }

            return Ok();
        }
    }
}
