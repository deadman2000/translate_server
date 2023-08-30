using SCI_Lib;
using SCI_Lib.Resources.Vocab;
using SCI_Lib.Resources;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Mongo;
using TranslateServer.Services;
using System.Linq;
using System.Collections.Generic;

namespace TranslateServer.Store
{
    public class WordsStore : MongoBaseService<WordDocument>
    {
        public WordsStore(MongoService mongo) : base(mongo, "Words")
        {
        }

        public async Task<IEnumerable<Resource>> Apply(SCIPackage package, string project)
        {
            var wordDocs = await Query(w => w.Project == project && w.IsTranslate);

            var res = (ResVocab001)package.AddResource(ResType.Vocabulary, 1);
            var words = wordDocs
                .SelectMany(doc => doc.Text.Split(',')
                .Select(word => new Word(word.Trim(), doc.WordId)));

            res.SetWords(words);

            return new Resource[] { res };
        }
    }
}
