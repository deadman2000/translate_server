using MongoDB.Bson.Serialization.Attributes;

namespace TranslateServer.Model
{
    public class TextResource : Document
    {
        public string Project { get; set; }

        public string Volume { get; set; }

        public int Number { get; set; }

        public string Text { get; set; }

        [BsonIgnoreIfNull]
        public int? Talker { get; set; }
    }
}
