using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class UsersService : MongoBaseService<User>
    {
        public UsersService(MongoService mongo) : base(mongo, "Users")
        {
        }
    }
}
