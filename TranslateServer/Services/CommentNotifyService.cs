using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class CommentNotifyService : MongoBaseService<CommentNotify>
    {
        public CommentNotifyService(MongoService mongo) : base(mongo, "CommentNotify")
        {
        }
    }
}
