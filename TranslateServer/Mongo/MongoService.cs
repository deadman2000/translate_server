using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace TranslateServer.Mongo
{
    public class MongoService
    {
        public MongoService(IConfiguration configuration)
        {
            var cs = configuration.GetConnectionString("Mongo");
            Client = new MongoClient(cs);
            Database = Client.GetDatabase("Translate");
        }

        public MongoClient Client { get; }
        public IMongoDatabase Database { get; }
    }
}
