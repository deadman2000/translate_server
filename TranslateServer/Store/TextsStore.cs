using MongoDB.Driver;
using TranslateServer.Documents;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class TextsStore : MongoBaseService<TextResource>
    {
        static bool IsInit = false;

        public TextsStore(MongoService mongo) : base(mongo, "Texts")
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
