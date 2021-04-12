using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class TranslateService : MongoBaseService<TextTranslate>
    {
        public TranslateService(MongoService mongo) : base(mongo, "Translate")
        {
        }

    }
}
