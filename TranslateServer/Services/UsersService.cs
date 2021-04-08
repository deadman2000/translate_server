using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class UsersService : MongoBaseService<UserDocument>
    {
        public UsersService(MongoService mongo) : base(mongo, "Users")
        {
        }
    }
}
