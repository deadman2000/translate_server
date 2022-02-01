using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class CommentsService : MongoBaseService<Comment>
    {
        public CommentsService(MongoService mongo) : base(mongo, "Comments")
        {
        }
    }
}
