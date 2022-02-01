using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TranslateServer.Helpers;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public abstract class MongoBaseService<T> where T : Document
    {
        protected readonly IMongoCollection<T> _collection;

        public MongoBaseService(MongoService mongo, string collectionName)
        {
            _collection = mongo.Database.GetCollection<T>(collectionName);
        }

        public IMongoCollection<T> Collection => _collection;

        public async Task<T> GetById(string id)
        {
            var cursor = await _collection.FindAsync(d => d.Id == id);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<T> Get(Expression<Func<T, bool>> filter)
        {
            var cursor = await _collection.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> All()
        {
            var cursor = await _collection.FindAsync(p => true);
            return cursor.ToEnumerable();
        }

        public async Task<IEnumerable<T>> Query(Expression<Func<T, bool>> filter)
        {
            var cursor = await _collection.FindAsync(filter);
            return cursor.ToEnumerable();
        }

        public MongoQuery<T> Query()
        {
            return new MongoQuery<T>(_collection);
        }

        public Task Insert(T doc)
        {
            return _collection.InsertOneAsync(doc);
        }

        public MongoUpdater<T> Update()
        {
            return new MongoUpdater<T>(_collection);
        }

        public MongoUpdater<T> Update(Expression<Func<T, bool>> filter)
        {
            return new MongoUpdater<T>(_collection).Where(filter);
        }

        public Task<DeleteResult> Delete(Expression<Func<T, bool>> filter)
        {
            return _collection.DeleteManyAsync(filter);
        }

    }
}
