using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TranslateServer.Model
{
    public class TextTranslate
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string Project { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string Volume { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string TextId { get; set; }

        public string Author { get; set; }

        public DateTime DateCreate { get; set; }

        public string Text { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string PrevId { get; set; }

        public bool Current { get; set; }

        public bool Correction { get; set; }

        public bool Deleted { get; set; }
    }
}
