using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class CommentNotifyStore : MongoBaseService<CommentNotify>
    {
        public CommentNotifyStore(MongoService mongo) : base(mongo, "CommentNotify")
        {
        }
    }
}
