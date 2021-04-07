﻿using MongoDB.Driver;
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
            var indexKeysDefinition = Builders<Project>.IndexKeys.Ascending(project => project.ShortName);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<Project>(indexKeysDefinition));
        }

        public Task<Project> GetProject(string shortName)
        {
            return Get(p => p.ShortName == shortName);
        }

        public MongoUpdater<Project> Update(string shortName)
        {
            return Update(p => p.ShortName == shortName);
        }
    }
}
