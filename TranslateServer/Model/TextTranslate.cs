using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TranslateServer.Model
{
    public class TextTranslate : Document
    {
        public string Project { get; set; }

        public string Resource { get; set; }

        public int Number { get; set; }

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
