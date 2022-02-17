using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class VideoTextService : MongoBaseService<VideoText>
    {
        public VideoTextService(MongoService mongo) : base(mongo, "VideoTexts")
        {
        }
    }
}
