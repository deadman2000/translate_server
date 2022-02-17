using Microsoft.AspNetCore.Mvc;
using System;
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
        private static readonly int FramesInTask = 50;

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
            var task = await _tasks.GetNext();

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

            public int Frames { get; set; }
        }

        [HttpPost("info")]
        public async Task<ActionResult> Info([FromBody] InfoRequest request)
        {
            var task = await _tasks.Get(t => t.Id == request.TaskId);
            if (task == null) return Ok();

            var result = await _tasks.Delete(request.TaskId);
            if (result.DeletedCount > 0)
            {
                await _videos.Update(v => v.VideoId == task.VideoId)
                    .Set(v => v.FramesCount, request.Frames)
                    .Execute();

                List<VideoTask> tasks = new();
                int from = 0;
                while (from < request.Frames)
                {
                    tasks.Add(new VideoTask
                    {
                        Type = VideoTask.GET_TEXT,
                        Project = task.Project,
                        VideoId = task.VideoId,
                        Frame = from,
                        Count = Math.Min(request.Frames - from, FramesInTask)
                    });

                    from += FramesInTask;
                }

                await _tasks.Insert(tasks);
            }

            return Ok();
        }

        public class TextsRequest
        {
            public string TaskId { get; set; }

            public IEnumerable<FrameTexts> Texts { get; set; }
        }

        public class FrameTexts
        {
            public int Frame { get; set; }
            public string Text { get; set; }
        }

        [HttpPost("texts")]
        public async Task<ActionResult> Texts([FromBody] TextsRequest request, [FromServices] VideoTextService videoText)
        {
            var task = await _tasks.Get(t => t.Id == request.TaskId);
            if (task == null) return Ok();

            var docs = request.Texts.Select(t => new VideoText
            {
                Project = task.Project,
                VideoId = task.VideoId,
                Frame = t.Frame,
                Text = t.Text
            });
            await videoText.Insert(docs);

            await _tasks.Delete(request.TaskId);
            return Ok();
        }
    }
}
