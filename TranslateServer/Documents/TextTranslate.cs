using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using TranslateServer.Model.Yandex;

namespace TranslateServer.Documents
{
    public class TextTranslate : Document
    {
        public static readonly IEqualityComparer<TextTranslate> Comparer = new TextTranslateComparer();

        public string Project { get; set; }

        public string Volume { get; set; }

        public int Number { get; set; }

        public string Author { get; set; }

        public string Editor { get; set; }

        public DateTime DateCreate { get; set; }

        public string Text { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string NextId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [BsonIgnoreIfDefault]
        [BsonRepresentation(BsonType.ObjectId)]
        public string FirstId { get; set; }

        public bool Deleted { get; set; }

        public int Letters { get; set; }

        public SpellResult[] Spellcheck { get; set; }

        public bool IsTranslate { get; set; }

        class TextTranslateComparer : IEqualityComparer<TextTranslate>
        {
            public bool Equals(TextTranslate x, TextTranslate y) => x.Project == y.Project && x.Volume == y.Volume && x.Number == y.Number;

            public int GetHashCode([DisallowNull] TextTranslate obj) => obj.Project.GetHashCode() ^ obj.Volume.GetHashCode() ^ obj.Number;
        }
    }
}
