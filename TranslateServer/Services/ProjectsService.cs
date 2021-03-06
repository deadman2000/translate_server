using MongoDB.Driver;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Helpers;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class ProjectsService : MongoBaseService<Project>
    {
        public ProjectsService(MongoService mongo) : base(mongo, "Projects")
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

        public async Task RecalcLetters(string shortName, VolumesService volumes)
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
    }
}
