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

        public async Task<ChartRow[]> GetChart(string login, string project = null)
        {
            var translates = await Query(t => t.Author == login && t.FirstId == null && !t.Deleted && (project == null || t.Project == project));
            var result = translates.Distinct(TextTranslate.Comparer)
                .GroupBy(t => t.DateCreate.Date)
                .OrderBy(g => g.Key)
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

                var trStrings = strings.ToList();

                foreach (var t in g)
                    trStrings[t.Number] = t.Text;

                if (trStrings.SequenceEqual(strings)) continue; // Skip not changed

                for (int i = 0; i < trStrings.Count; i++)
                {
                    const string PartSeparator = "$next";
                    var translate = trStrings[i];

                    if (translate.Contains(PartSeparator) && res is ResMessage resMsg)
                    {
                        var parts = translate.Split(PartSeparator);
                        var records = resMsg.GetMessages();
                        var record = records[i];

                        var sequence = records.Where(r => r.Noun == record.Noun && r.Verb == record.Verb && r.Cond == record.Cond).ToArray();
                        if (sequence.Any(r => r.Seq == record.Seq + 1))
                        {
                            // Сдвигаем все следующие сообщения
                            foreach (var rec in sequence)
                            {
                                if (rec.Seq > record.Seq)
                                {
                                    rec.Seq++;
                                }
                            }
                        }

                        if (record is MessageRecordV4)
                        {
                            for (int j = 0; j < parts.Length - 1; j++)
                            {
                                records.Add(new MessageRecordV4(record.Noun, record.Verb, record.Cond, (byte)(record.Seq + 1), record.Talker, ""));
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }

                        trStrings[i] = enc.UnescapeString(parts[0]);

                        for (int j = 1; j < parts.Length; j++)
                        {
                            trStrings.Add(enc.UnescapeString(parts[j]));
                        }
                    }
                    else
                    {
                        trStrings[i] = enc.UnescapeString(translate);
                    }
                }

                res.SetStrings(trStrings.ToArray());
                resources.Add(res);
            }
            return resources;
        }
    }
}
