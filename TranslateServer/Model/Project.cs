using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TranslateServer.Model
{
    public class Project : Document
    {
        public string Name { get; set; }

        public string Code { get; set; }

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
    }

    public enum ProjectStatus
    {
        New = 0,
        Processing = 1,
        Working = 2,
        Completed = 3,
        Error = 4,
    }
}
