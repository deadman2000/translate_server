using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Text.Json.Serialization;

namespace TranslateServer.Model
{
    public class Comment : Document
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string TranslateId { get; set; }

        [JsonIgnore]
        public string Project { get; set; }

        [JsonIgnore]
        public string Volume { get; set; }

        public string Author { get; set; }

        public DateTime DateCreate { get; set; }

        public string Text { get; set; }
    }
}
