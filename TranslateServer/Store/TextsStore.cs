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
                var projIndex = Builders<TextResource>.IndexKeys.Ascending(t => t.Project);
                var projVolNumIndex = Builders<TextResource>.IndexKeys.Ascending(t => t.Project).Ascending(t => t.Volume).Ascending(t => t.Number);
                _collection.Indexes.CreateManyAsync(new CreateIndexModel<TextResource>[] { new(projIndex), new (projVolNumIndex) });
                IsInit = true;
            }
        }
    }
}
