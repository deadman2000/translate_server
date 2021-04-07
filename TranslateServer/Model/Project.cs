﻿namespace TranslateServer.Model
{
    public class Project : Document
    {
        public string Name { get; set; }

        public string ShortName { get; set; }

        public ProjectStatus Status { get; set; }
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
