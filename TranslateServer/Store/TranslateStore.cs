using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class TranslateStore : MongoBaseService<TextTranslate>
    {
        public TranslateStore(MongoService mongo) : base(mongo, "Translate")
        {
        }

        public async Task<int> GetUserLetters(string login)
        {
            var translates = await Query(t => t.Author == login && t.NextId == null && !t.Deleted);
            return translates.Distinct(TextTranslate.Comparer).Sum(t => t.Letters);
        }

        public async Task<ChartRow[]> GetChart(string login)
        {
            var translates = await Query(t => t.Author == login && t.FirstId == null && !t.Deleted);
            var result = translates.Distinct(TextTranslate.Comparer)
                .GroupBy(t => t.DateCreate.Date)
                .Select(g => new ChartRow
                {
                    D = ((DateTimeOffset)g.Key).ToUnixTimeSeconds(),
                    L = g.Sum(t => t.Letters)
                })
                .ToArray();

            for (int i = 0; i < result.Length - 1; i++)
                result[i + 1].L += result[i].L;
            return result;
        }

        public class ChartRow
        {
            public long D { get; set; }
            public int L { get; set; }
        }
    }
}
