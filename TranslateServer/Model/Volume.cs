﻿namespace TranslateServer.Model
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

        public int NumberOfLetters { get; set; }
    }
}
