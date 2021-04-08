﻿using MongoDB.Driver;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class TextsService : MongoBaseService<TextResource>
    {
        public TextsService(MongoService mongo) : base(mongo, "Texts")
        {
            var indexKeysDefinition = Builders<TextResource>.IndexKeys.Ascending(t => t.Project);
            _collection.Indexes.CreateOneAsync(new CreateIndexModel<TextResource>(indexKeysDefinition));
        }
    }
}