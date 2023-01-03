using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class UsersStore : MongoBaseService<UserDocument>
    {
        public UsersStore(MongoService mongo) : base(mongo, "Users")
        {
        }
    }
}
