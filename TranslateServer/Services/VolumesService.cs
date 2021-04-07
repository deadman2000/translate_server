using MongoDB.Driver;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class VolumesService : MongoBaseService<Volume>
    {
        public VolumesService(MongoService mongo) : base(mongo, "Volumes")
        {
            var indexKeysDefinition = Builders<Volume>.IndexKeys.Ascending(v => v.Project);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<Volume>(indexKeysDefinition));
        }
    }
}
