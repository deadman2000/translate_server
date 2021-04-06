using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TranslateServer.Model
{
    public class TextResource
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string Project { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string Volume { get; set; }

        public int Order { get; set; }

        public string Text { get; set; }

        public int Talker { get; set; }
    }
}
