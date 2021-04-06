using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TranslateServer.Model
{
    public class Volume
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string Project { get; set; }

        public string Name { get; set; }
    }
}
