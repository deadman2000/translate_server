using SCI_Lib;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class SaidStore : MongoBaseService<SaidDocument>
    {
        public SaidStore(MongoService mongo) : base(mongo, "Saids")
        {
        }

        public async Task<IEnumerable<Resource>> Apply(SCIPackage package, string project)
        {
            List<Resource> resources = new();
            var saids = await Query(s => s.Project == project && s.Patch != null);

            foreach (var gr in saids.GroupBy(s => s.Script))
            {
                var res = package.GetResource<ResScript>((ushort)gr.Key);
                var scr = res.GetScript() as Script;
                var ss = scr.SaidSection;

                bool changed = false;
                foreach (var doc in gr)
                {
                    if (ss.Saids[doc.Index].Set(doc.Patch))
                        changed = true;
                }

                if (changed)
                    resources.Add(res);
            }

            return resources;
        }
    }
}
