using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TranslateServer.Model
{
    public class Project
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public bool Uploaded { get; set; }
    }
}
