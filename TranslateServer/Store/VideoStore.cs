using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class VideoStore : MongoBaseService<Video>
    {
        public VideoStore(MongoService mongo) : base(mongo, "Videos")
        {
        }
    }
}
