using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class VideoService : MongoBaseService<Video>
    {
        public VideoService(MongoService mongo) : base(mongo, "Videos")
        {
        }
    }
}
