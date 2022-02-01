using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TranslateServer.Model
{
    public class Comment : Document
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string TranslateId { get; set; }

        public string Author { get; set; }

        public DateTime DateCreate { get; set; }

        public string Text { get; set; }
    }
}
