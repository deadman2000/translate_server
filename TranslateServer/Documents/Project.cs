using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TranslateServer.Documents
{
    public class Project : Document
    {
        public string Name { get; set; }

        public string Code { get; set; }

        public string Engine { get; set; }

        public ProjectStatus Status { get; set; }

        [BsonIgnoreIfNull]
        public string Error { get; set; }

        public int Letters { get; set; }

        public int Texts { get; set; }

        public DateTime? LastSubmit { get; set; }

        public int TranslatedLetters { get; set; }

        public int TranslatedTexts { get; set; }

        public int ApprovedTexts { get; set; }

        public int ApprovedLetters { get; set; }

        public bool Shared { get; set; }
    }

    public enum ProjectStatus
    {
        New = 0,
        TextExtract = 1,
        ResourceExtract = 2,
        Indexing = 3,
        Ready = 4,
        Error = 5,
    }
}
