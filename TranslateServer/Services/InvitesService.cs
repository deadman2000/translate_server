using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class InvitesService : MongoBaseService<Invite>
    {
        public InvitesService(MongoService mongo) : base(mongo, "Invites")
        {
        }
    }
}
