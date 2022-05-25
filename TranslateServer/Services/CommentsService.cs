using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Mongo;

namespace TranslateServer.Services
{
    public class CommentsService : MongoBaseService<Comment>
    {
        public CommentsService(MongoService mongo) : base(mongo, "Comments")
        {
        }

        public Task<List<Comment>> GetComments(TextTranslate translate)
        {
            return Query(c => c.TranslateId == translate.Id || c.TranslateId == translate.FirstId);
        }
    }
}
