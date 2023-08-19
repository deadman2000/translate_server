using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Helpers;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class ProjectsStore : MongoBaseService<Project>
    {
        public ProjectsStore(MongoService mongo) : base(mongo, "Projects")
        {
            var indexKeysDefinition = Builders<Project>.IndexKeys.Ascending(project => project.Code);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<Project>(indexKeysDefinition));
        }

        public Task<Project> GetProject(string shortName)
        {
            return Get(p => p.Code == shortName);
        }

        public MongoUpdater<Project> Update(string shortName)
        {
            return Update(p => p.Code == shortName);
        }

        public async Task RecalcLetters(string shortName, VolumesStore volumes)
        {
            var res = await volumes.Collection.Aggregate()
                .Match(v => v.Project == shortName)
                .Group(v => v.Project,
                g => new
                {
                    Total = g.Sum(t => t.Letters),
                    Count = g.Sum(t => t.Texts)
                })
                .FirstOrDefaultAsync();

            await Update(p => p.Code == shortName)
                .Set(p => p.Letters, res.Total)
                .Set(p => p.Texts, res.Count)
                .Execute();
        }

        public Task<List<Project>> Shared()
        {
            return Query(p => p.Shared == true);
        }
    }
}
