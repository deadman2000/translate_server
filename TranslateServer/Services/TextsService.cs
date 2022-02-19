using MongoDB.Driver;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class TextsService : MongoBaseService<TextResource>
    {
        static bool IsInit = false;

        public TextsService(MongoService mongo) : base(mongo, "Texts")
        {
            if (!IsInit)
            {
                var indexKeysDefinition = Builders<TextResource>.IndexKeys.Ascending(t => t.Project);
                _collection.Indexes.CreateOneAsync(new CreateIndexModel<TextResource>(indexKeysDefinition));
                IsInit = true;
            }
        }
    }
}
