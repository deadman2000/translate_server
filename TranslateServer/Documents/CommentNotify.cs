using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TranslateServer.Documents
{
    public class CommentNotify : Document
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string CommentId { get; set; }

        public string User { get; set; }

        public DateTime Date { get; set; }

        public bool Read { get; set; }
    }
}
