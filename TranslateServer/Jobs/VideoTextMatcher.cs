using MongoDB.Driver;
using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            if (Debugger.IsAttached)
            {
                q.UseMicrosoftDependencyInjectionJobFactory();
                q.UseDefaultThreadPool(x => { x.MaxConcurrency = 1; });
                q.ScheduleJob<VideoTextMatcher>(j => j
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithIntervalInSeconds(5)
                        .RepeatForever())
                );
            }
            else
            {
                q.UseMicrosoftDependencyInjectionJobFactory();
                q.UseDefaultThreadPool(x => { x.MaxConcurrency = 1; });
                q.ScheduleJob<VideoTextMatcher>(j => j
                    .StartAt(DateTimeOffset.UtcNow.AddMinutes(1))
                    .WithSimpleSchedule(x => x
                        .WithIntervalInMinutes(1)
                        .RepeatForever())
                );
            }
        }

        private readonly VideoTextService _videoText;
        private readonly TextsService _texts;
        private readonly VideoReferenceService _videoReference;
        private readonly SearchService _search;
        private readonly VideoService _videos;
        private readonly VideoTasksService _tasks;

        public VideoTextMatcher(VideoTextService videoText, TextsService texts, VideoReferenceService videoReference, SearchService search, VideoService videos, VideoTasksService tasks)
        {
            _videoText = videoText;
            _texts = texts;
            _videoReference = videoReference;
            _search = search;
            _videos = videos;
            _tasks = tasks;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await CalcMaxScoore();
            await ProcessTexts();
            await CompleteVideos();
        }

        private async Task CalcMaxScoore()
        {
            while (true)
            {
                var texts = (await _texts.Query().Where(t => t.MaxScore == null).Limit(10).Execute()).ToArray();
                if (texts.Length == 0) break;

                await Task.WhenAll(texts.Select(t => CalcScore(t)).ToArray());
            }
        }

        private async Task ProcessTexts()
        {
            while (true)
            {
                var videoTexts = (await _videoText.Query().Limit(10).Execute()).ToArray();
                if (videoTexts.Length == 0) break;
                await Task.WhenAll(videoTexts.Select(vt => Process(vt)).ToArray());
            }
        }

        private async Task CompleteVideos()
        {
            var videos = await _videos.Query(v => !v.Completed && v.FramesCount > 0 && v.FramesProcessed > 0);
            foreach (var v in videos)
            {
                if (v.FramesProcessed >= v.FramesCount)
                    await CompleteVideo(v);
            }
        }

        private async Task CompleteVideo(Video video)
        {
            var refs = await _videoReference.Query(r => r.VideoId == video.VideoId);

            var frames = refs.Select(r => r.Frame).Distinct();

            var tasks = frames
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / 10)
                .Select(x => x.Select(v => v.Value).ToList())
                .Select(x => new VideoTask
                {
                    Type = VideoTask.GET_IMAGE,
                    VideoId = video.VideoId,
                    Project = video.Project,
                    Frames = x.ToArray(),
                }).ToArray();

            if (tasks.Length > 0)
                await _tasks.Insert(tasks);

            await _tasks.Delete(t => t.VideoId == video.VideoId && t.Type != VideoTask.GET_IMAGE);
            await _videos.Update().Where(v => v.Id == video.Id).Set(v => v.Completed, true).Execute();
        }

        private async Task CalcScore(TextResource text)
        {
            var score = await _search.GetMaxScore(text);
            if (score == 0) return;
            await _texts.Update(t => t.Id == text.Id).Set(t => t.MaxScore, score).Execute();
        }

        private async Task Process(VideoText vt)
        {
            var matches = await _search.GetMatch(vt.Project, vt.Text);
            await Task.WhenAll(matches.Select(m => ProcessMatch(vt, m)).ToArray());
            await _videoText.DeleteOne(v => v.Id == vt.Id);
        }

        private async Task ProcessMatch(VideoText vt, MatchResult m)
        {
            var txt = await _texts.Get(t => t.Project == vt.Project && t.Volume == m.Volume && t.Number == m.Number);
            if (txt.MaxScore.HasValue)
            {
                if (m.Score < txt.MaxScore * 0.6) return;
                if (m.Score > txt.MaxScore * 1.2) return;
            }

            var matchRate = m.Score / txt.MaxScore;

            var reference = await _videoReference.Create(vt.Project, m.Volume, m.Number, vt.VideoId);
            if (reference.Score > m.Score) return;

            await _videoReference.Update(r => r.Id == reference.Id && (r.Score == null || r.Score < m.Score))
                .Set(r => r.Frame, vt.Frame)
                .Set(r => r.T, vt.T)
                .Set(r => r.Score, m.Score)
                .Set(r => r.Rate, matchRate)
                .Execute();

            var maxScore = _videoReference.Collection.AsQueryable()
                .Where(r => r.Project == vt.Project && r.Volume == m.Volume && r.Number == m.Number)
                .Max(r => r.Score);
            var scoreThr = maxScore * 0.8;

            await _videoReference.Delete(r => r.Project == vt.Project && r.Volume == m.Volume && r.Number == m.Number && r.Score < scoreThr);
        }
    }
}
