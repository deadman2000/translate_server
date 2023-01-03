using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class VideoTasksStore : MongoBaseService<VideoTask>
    {
        private static readonly TimeSpan TimeOut = TimeSpan.FromMinutes(15);

        private static readonly int FramesInTask = 10;

        private static readonly int FrameSkip = 10;

        public VideoTasksStore(MongoService mongo) : base(mongo, "VideoTasks")
        {
        }

        public Task CreateGetInfo(string project, string videoId)
        {
            return Insert(new VideoTask
            {
                Type = VideoTask.INFO_REQUEST,
                VideoId = videoId,
                Project = project,
            });
        }

        public Task CreateGetFrames(string project, string videoId, int frames)
        {
            List<VideoTask> tasks = new();
            int from = 0;
            while (from < frames)
            {
                tasks.Add(new VideoTask
                {
                    Type = VideoTask.GET_TEXT,
                    Project = project,
                    VideoId = videoId,
                    Frame = from,
                    Count = Math.Min(frames - from, FramesInTask),
                    FrameSkip = FrameSkip
                });

                from += FramesInTask * FrameSkip;
            }

            return Insert(tasks);
        }

        public Task<VideoTask> GetNext(string runner)
        {
            var now = DateTime.UtcNow;
            return Update(t => !t.Completed && (t.LastRequest == null || t.LastRequest < now.Subtract(TimeOut)))
                .Set(t => t.LastRequest, now)
                .Set(t => t.Runner, runner)
                .Get(new FindOneAndUpdateOptions<VideoTask, VideoTask>
                {
                    Sort = new SortDefinitionBuilder<VideoTask>().Ascending(t => t.LastRequest)
                });
        }

        public Task<UpdateResult> Complete(string taskId, string runnerId)
        {
            return Update(t => t.Id == taskId && !t.Completed)
                .Set(t => t.Completed, true)
                .Set(t => t.Runner, runnerId)
                .Set(t => t.DateComplete, DateTime.UtcNow)
                .Execute();
        }
    }
}
