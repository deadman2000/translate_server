using SCI_Lib;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Vocab;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class SuffixesStore : MongoBaseService<SuffixDocument>
    {
        public SuffixesStore(MongoService mongo) : base(mongo, "Suffixes")
        {
        }

        public async Task<Resource> Apply(SCIPackage package, string project)
        {
            var suffDocs = await Query(w => w.Project == project && w.IsTranslate);
            
            var voc = (ResVocab901)package.GetResource(ResType.Vocabulary, 901);
            var src = voc.GetSuffixes();

            var newList = src.Concat(suffDocs.Select(s => new Suffix(s.Output, (ushort)s.OutClass, s.Input, (ushort)s.InClass)));
            var arr = newList.ToArray();
            voc.SetSuffixes(arr);

            return voc;
        }
    }
}
