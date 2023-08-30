using SCI_Lib;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using SCI_Lib.Resources.Scripts.Sections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class SynonymStore : MongoBaseService<SynonymDocument>
    {
        public SynonymStore(MongoService mongo) : base(mongo, "Synonyms")
        {
        }

        public async Task<IEnumerable<Resource>> Apply(SCIPackage package, string project)
        {
            List<Resource> resources = new();
            var synonyms = await Query(s => s.Project == project && s.Delete || s.Index == null);

            foreach (var gr in synonyms.GroupBy(s => s.Script))
            {
                var res = package.GetResource<ResScript>((ushort)gr.Key);
                var scr = res.GetScript() as Script;
                var ss = scr.SynonymSecion;

                var toRemove = gr.Where(s => s.Delete)
                    .Select(s => ss.Synonyms[s.Index.Value])
                    .ToList();
                foreach (var s in toRemove)
                    ss.Synonyms.Remove(s);

                foreach (var doc in gr.Where(s => !s.Delete))
                {
                    ss.Synonyms.Add(new Synonym
                    {
                        WordA = doc.WordA,
                        WordB = doc.WordB
                    });
                }

                resources.Add(res);
            }

            return resources;
        }
    }
}
