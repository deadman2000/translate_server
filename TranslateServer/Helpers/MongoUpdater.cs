using MongoDB.Driver;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace TranslateServer.Helpers
{
    public class MongoUpdater<T>
    {
        private readonly IMongoCollection<T> _collection;
        private Expression<Func<T, bool>> _filter;
        private UpdateDefinition<T> _update;

        public MongoUpdater(IMongoCollection<T> collection)
        {
            _collection = collection;
        }

        public MongoUpdater<T> Where(Expression<Func<T, bool>> filter)
        {
            _filter = filter;
            return this;
        }

        public MongoUpdater<T> Set<TField>(Expression<Func<T, TField>> field, TField value)
        {
            if (_update == null)
                _update = Builders<T>.Update.Set(field, value);
            else
                _update = _update.Set(field, value);

            return this;
        }

        public MongoUpdater<T> Unset<TField>(Expression<Func<T, TField>> field)
        {
            var s = new ExpressionFieldDefinition<T, TField>(field);

            if (_update == null)
                _update = Builders<T>.Update.Unset(s);
            else
                _update = _update.Unset(s);

            return this;
        }

        public MongoUpdater<T> Inc<TField>(Expression<Func<T, TField>> field, TField value)
        {
            if (_update == null)
                _update = Builders<T>.Update.Inc(field, value);
            else
                _update = _update.Inc(field, value);

            return this;
        }

        public Task<UpdateResult> Execute()
        {
            if (_update == null) return Task.FromResult<UpdateResult>(null);
            return _collection.UpdateOneAsync(_filter, _update);
        }

        public Task<UpdateResult> ExecuteMany()
        {
            if (_update == null) return Task.FromResult<UpdateResult>(null);
            return _collection.UpdateManyAsync(_filter, _update);
        }

        public Task<T> Get()
        {
            return _collection.FindOneAndUpdateAsync(_filter, _update);
        }

        public Task<T> Get(FindOneAndUpdateOptions<T, T> op)
        {
            return _collection.FindOneAndUpdateAsync(_filter, _update, op);
        }

        public Task<UpdateResult> Upsert()
        {
            return _collection.UpdateOneAsync(_filter, _update, new UpdateOptions { IsUpsert = true });
        }
    }
}
