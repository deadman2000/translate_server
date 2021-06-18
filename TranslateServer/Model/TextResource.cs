using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TranslateServer.Model
{
    public class TextResource : Document
    {
        public TextResource()
        {
        }

        public TextResource(Project project, Volume volume, int number, string text)
        {
            Project = project.Code;
            Volume = volume.Code;
            Number = number;
            Text = text;
            Letters = CalcLetters(text);
        }

        public string Project { get; set; }

        public string Volume { get; set; }

        public int Number { get; set; }

        public string Text { get; set; }

        [BsonIgnoreIfNull]
        public int? Talker { get; set; }

        [BsonIgnoreIfNull]
        public int? Verb { get; set; }

        [BsonIgnoreIfNull]
        public List<string> Noun { get; set; }

        public int Letters { get; set; }


        private static readonly Regex NotLetters = new("[^\\w]");

        private static int CalcLetters(string text)
        {
            return NotLetters.Replace(text, "").Length;
        }

        public bool HasTranslate { get; set; }
    }
}
