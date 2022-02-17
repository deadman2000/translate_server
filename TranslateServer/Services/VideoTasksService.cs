using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class VideoTasksService : MongoBaseService<VideoTask>
    {
        private static readonly TimeSpan TimeOut = TimeSpan.FromMinutes(5);

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

        public Task<DeleteResult> Delete(string id) => DeleteOne(t => t.Id == id);

        public Task<VideoTask> GetNext()
        {
            var now = DateTime.UtcNow;
            return Update(t => t.LastRequest == null || t.LastRequest < now.Subtract(TimeOut))
                .Set(t => t.LastRequest, now)
                .Get();
        }
    }
}
