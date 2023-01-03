using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class VideoTextStore : MongoBaseService<VideoText>
    {
        public VideoTextStore(MongoService mongo) : base(mongo, "VideoTexts")
        {
        }
    }
}
