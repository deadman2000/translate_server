using TranslateServer.Documents;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class InvitesStore : MongoBaseService<Invite>
    {
        public InvitesStore(MongoService mongo) : base(mongo, "Invites")
        {
        }
    }
}
