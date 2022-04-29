using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using System.Diagnostics;

namespace TranslateServer.Mongo
{
    public class MongoService
    {
        public MongoService(IConfiguration configuration, ILogger<MongoService> logger)
        {
            var cs = configuration.GetConnectionString("Mongo");
            var mongoClientSettings = MongoClientSettings.FromUrl(new MongoUrl(cs));

            /*if (Debugger.IsAttached)
                mongoClientSettings.ClusterConfigurator = cb =>
                {
                    cb.Subscribe<CommandStartedEvent>(e =>
                    {
                        if (e.Command.ElementCount > 0)
                            logger.LogDebug("{CommandName} {CommandJson}", e.CommandName, e.Command);
                    });
                };*/

            Client = new MongoClient(mongoClientSettings);
            Database = Client.GetDatabase("Translate");
        }

        public MongoClient Client { get; }
        public IMongoDatabase Database { get; }
    }
}
