using MongoDB.Driver;
using SCI_Lib;
using SCI_Lib.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
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

        public async Task<IEnumerable<ProjectLetters>> GetUserLetters(string login)
        {
            var translates = await Query(t => t.Author == login && t.NextId == null && !t.Deleted);
            return translates.GroupBy(t => t.Project).Select(gr => new ProjectLetters
            {
                Project = gr.Key,
                Letters = gr.Distinct(TextTranslate.Comparer).Sum(t => t.Letters)
            });
        }

        public class ChartRow
        {
            public long D { get; set; }
            public int L { get; set; }
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

        public async Task<IEnumerable<Resource>> Apply(SCIPackage package, string project)
        {
            List<Resource> resources = new();
            var enc = package.GameEncoding;
            var texts = await Query(t => t.Project == project && !t.Deleted && t.NextId == null);
            foreach (var g in texts.GroupBy(t => t.Volume))
            {
                var resourceName = g.Key.Replace('_', '.');
                var res = package.GetResource(resourceName);
                var strings = res.GetStrings();
                for (int i = 0; i < strings.Length; i++)
                    strings[i] = enc.EscapeString(strings[i]);

                var trStrings = (string[])strings.Clone();

                foreach (var t in g)
                    trStrings[t.Number] = t.Text;

                if (trStrings.SequenceEqual(strings)) continue; // Skip not changed

                for (int i = 0; i < trStrings.Length; i++)
                    trStrings[i] = enc.UnescapeString(trStrings[i]);

                res.SetStrings(trStrings);
                resources.Add(res);
            }
            return resources;
        }
    }
}
