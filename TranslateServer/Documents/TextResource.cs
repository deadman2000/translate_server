using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;

namespace TranslateServer.Documents
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

        public int Letters { get; set; }

        public void RecalcLetters()
        {
            Letters = CalcLetters(Text);
        }


        private static readonly Regex NotLetters = new("[ \\t]");

        private static int CalcLetters(string text)
        {
            return NotLetters.Replace(text, "").Length;
        }

        public bool HasTranslate { get; set; }

        public bool TranslateApproved { get; set; }

        [BsonIgnoreIfNull]
        public double? MaxScore { get; set; }

        public string Description { get; set; }
    }
}
