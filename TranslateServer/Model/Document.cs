using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TranslateServer.Model
{
    [BsonIgnoreExtraElements(Inherited = true)]
    public class Document
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
    }
}
