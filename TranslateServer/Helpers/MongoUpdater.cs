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

        public Task<UpdateResult> Execute()
        {
            return _collection.UpdateOneAsync(_filter, _update);
        }

        public Task<UpdateResult> ExecuteMany()
        {
            return _collection.UpdateManyAsync(_filter, _update);
        }
    }
}
