using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Jobs
{
    public class VideoTextMatcher : IJob
    {
        public static void Schedule(IServiceCollectionQuartzConfigurator q)
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            q.UseDefaultThreadPool(x => { x.MaxConcurrency = 1; });
            q.ScheduleJob<VideoTextMatcher>(j => j
                //.StartAt(DateTimeOffset.UtcNow.AddMinutes(1))
                .StartNow()
                .WithSimpleSchedule(x => x
                    //.WithIntervalInMinutes(1)
                    .WithIntervalInSeconds(5)
                    .RepeatForever())
            );
        }

        private readonly ILogger<VideoTextMatcher> _logger;
        private readonly VideoTextService _videoText;
        private readonly TextsService _texts;
        private readonly SearchService _search;

        public VideoTextMatcher(ILogger<VideoTextMatcher> logger, VideoTextService videoText, TextsService texts, SearchService search)
        {
            _logger = logger;
            _videoText = videoText;
            _texts = texts;
            _search = search;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            while (true)
            {
                var videoTexts = await _videoText.All();
                if (!videoTexts.Any()) return;
                await Task.WhenAll(videoTexts.Select(vt => Process(vt)).ToArray());
            }
        }

        private async Task Process(VideoText vt)
        {
            var matches = await _search.GetMatch(vt.Project, vt.Text);
            await Task.WhenAll(matches.Select(m => ProcessMatch(vt, m)).ToArray());
            await _videoText.DeleteOne(v => v.Id == vt.Id);
        }

        private async Task ProcessMatch(VideoText vt, MatchResult m)
        {
            var res = await _texts.Get(t => t.Project == vt.Project && t.Volume == m.Volume && t.Number == m.Number);
            if (res == null) return;

            if (res.References == null)
                res.References = new List<VideoReference>();

            VideoReference newRef;
            var oldRef = res.References.Find(r => r.VideoId == vt.VideoId);
            if (oldRef == null)
            {
                newRef = new VideoReference
                {
                    VideoId = vt.VideoId,
                    Frame = vt.Frame,
                    Score = m.Score
                };
                res.References = new List<VideoReference> { newRef };
            }
            else if (oldRef.Score < m.Score)
            {
                res.References.Remove(oldRef);
                res.References.Add(newRef = new VideoReference
                {
                    VideoId = vt.VideoId,
                    Frame = vt.Frame,
                    Score = m.Score
                });
            }
            else
                return; // Old ref is best

            var maxScore = res.References.Max(r => r.Score);
            res.References.RemoveAll(s => s.Score < maxScore * 0.8);
            if (!res.References.Contains(newRef)) return; // No changes

            await _texts.Update()
                .Where(t => t.Id == res.Id)
                .Set(t => t.References, res.References)
                .Execute();
        }
    }
}
