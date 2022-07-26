using System;

namespace TranslateServer.Model
{
    public class Volume : Document
    {
        public Volume()
        {
        }

        public Volume(Project project, string name)
        {
            Project = project.Code;
            Name = name;
            Code = name.ToLower().Replace('.', '_');
        }

        public string Project { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int Letters { get; set; }

        public int Texts { get; set; }

        public DateTime? LastSubmit { get; set; }

        public int TranslatedTexts { get; set; }

        public int TranslatedLetters { get; set; }

        public int ApprovedTexts { get; set; }

        public int ApprovedLetters { get; set; }
    }
}
