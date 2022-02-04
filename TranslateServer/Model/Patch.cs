using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TranslateServer.Model
{
    public class Patch : Document
    {
        public string Project { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string FileId { get; set; }
    }
}
