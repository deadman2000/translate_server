using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TranslateServer.Model
{
    public class TextTranslate : Document
    {
        public static readonly IEqualityComparer<TextTranslate> Comparer = new TextTranslateComparer();

        public string Project { get; set; }

        public string Volume { get; set; }

        public int Number { get; set; }

        public string Author { get; set; }

        public DateTime DateCreate { get; set; }

        public string Text { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string NextId { get; set; }

        public bool Deleted { get; set; }

        public int Comments { get; set; }

        public int Letters { get; set; }

        class TextTranslateComparer : IEqualityComparer<TextTranslate>
        {
            public bool Equals(TextTranslate x, TextTranslate y)
            {
                return x.Project == y.Project && x.Volume == y.Volume && x.Number == y.Number;
            }

            public int GetHashCode([DisallowNull] TextTranslate obj)
            {
                return obj.Project.GetHashCode() ^ obj.Volume.GetHashCode() ^ obj.Number;
            }
        }
    }
}
