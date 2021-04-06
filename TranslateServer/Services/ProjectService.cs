using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class ProjectService
    {
        private readonly IMongoCollection<Project> _projects;

        public ProjectService(MongoService mongo)
        {
            _projects = mongo.Database.GetCollection<Project>("Projects");

            var indexKeysDefinition = Builders<Project>.IndexKeys.Ascending(project => project.ShortName);
            _projects.Indexes.CreateOneAsync(new CreateIndexModel<Project>(indexKeysDefinition));
        }

        public Task Create(Project project)
        {
            return _projects.InsertOneAsync(project);
        }

        public async Task<IEnumerable<Project>> GetProjects()
        {
            var cursor = await _projects.FindAsync(p => true);
            return cursor.ToEnumerable();
        }

        public async Task<Project> GetProject(string shortName)
        {
            var cursor = await _projects.FindAsync(p => p.ShortName == shortName);
            return await cursor.FirstOrDefaultAsync();
        }
    }
}
