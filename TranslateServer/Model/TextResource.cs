using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;

namespace TranslateServer.Model
{
    public class TextResource : Document
    {
        public TextResource()
        {
        }

        public TextResource(Project project, string volume, int number, string text, int? talker = null)
        {
            Project = project.ShortName;
            Volume = volume;
            Number = number;
            Text = text;
            Talker = talker;
            NumberOfLetters = CalcLetters(text);
        }

        public string Project { get; set; }

        public string Volume { get; set; }

        public int Number { get; set; }

        public string Text { get; set; }

        [BsonIgnoreIfNull]
        public int? Talker { get; set; }

        public int NumberOfLetters { get; set; }


        private static readonly Regex NotLetters = new("[^\\w]");

        private static int CalcLetters(string text)
        {
            return NotLetters.Replace(text, "").Length;
        }
    }
}
