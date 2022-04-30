using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class TranslateService : MongoBaseService<TextTranslate>
    {
        public TranslateService(MongoService mongo) : base(mongo, "Translate")
        {
        }

        public async Task<int> GetUserLetters(string login)
        {
            var translates = await Query(t => t.Author == login && t.NextId == null && !t.Deleted);
            return translates.Distinct(TextTranslate.Comparer).Sum(t => t.Letters);
        }
    }
}
