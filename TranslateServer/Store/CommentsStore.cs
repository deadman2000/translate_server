using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;
using TranslateServer.Services;

namespace TranslateServer.Store
{
    public class CommentsStore : MongoBaseService<Comment>
    {
        public CommentsStore(MongoService mongo) : base(mongo, "Comments")
        {
        }

        public Task<List<Comment>> GetComments(TextTranslate translate)
        {
            return Query(c => c.TranslateId == translate.Id || c.TranslateId == translate.FirstId);
        }
    }
}
