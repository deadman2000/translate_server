using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace TranslateServer.Helpers
{
    public class MongoQuery<T>
    {
        private readonly IMongoCollection<T> _collection;
        private readonly List<FilterDefinition<T>> filters = new();
        private SortDefinition<T> _sort;

        public MongoQuery(IMongoCollection<T> collection)
        {
            _collection = collection;
        }

        public MongoQuery<T> Where(Expression<Func<T, bool>> filter)
        {
            filters.Add(Builders<T>.Filter.Where(filter));
            return this;
        }

        public MongoQuery<T> SortAsc(Expression<Func<T, object>> field)
        {
            _sort = Builders<T>.Sort.Ascending(field);
            return this;
        }

        public async Task<IEnumerable<T>> Execute()
        {
            if (filters.Count == 0)
                throw new InvalidOperationException();

            FilterDefinition<T> filter;
            if (filters.Count == 1)
                filter = filters[0];
            else
                filter = Builders<T>.Filter.And(filters);

            FindOptions<T, T> options = null;
            if (_sort != null)
            {
                options = new FindOptions<T, T>()
                {
                    Sort = _sort
                };
            }

            var result = await _collection.FindAsync(filter, options);
            return result.ToEnumerable();
        }
    }
}
