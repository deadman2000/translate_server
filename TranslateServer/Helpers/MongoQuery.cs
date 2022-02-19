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
        private int? _limit;

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

        public MongoQuery<T> Limit(int value)
        {
            _limit = value;
            return this;
        }

        public async Task<IEnumerable<T>> Execute()
        {
            FilterDefinition<T> filter;
            if (filters.Count == 0)
                filter = Builders<T>.Filter.Empty;
            else if (filters.Count == 1)
                filter = filters[0];
            else
                filter = Builders<T>.Filter.And(filters);

            FindOptions<T, T> options = new()
            {
                Sort = _sort,
                Limit = _limit
            };
            var result = await _collection.FindAsync(filter, options);
            return result.ToEnumerable();
        }
    }
}
