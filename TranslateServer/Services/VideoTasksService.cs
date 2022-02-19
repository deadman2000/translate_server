using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class VideoTasksService : MongoBaseService<VideoTask>
    {
        private static readonly TimeSpan TimeOut = TimeSpan.FromMinutes(15);

        public VideoTasksService(MongoService mongo) : base(mongo, "VideoTasks")
        {
        }

        public Task GetInfo(string videoId, string project)
        {
            return Insert(new VideoTask
            {
                Type = VideoTask.INFO_REQUEST,
                VideoId = videoId,
                Project = project,
            });
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
