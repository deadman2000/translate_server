namespace TranslateServer.Model
{
    public class Video : Document
    {
        public string Project { get; set; }

        public string VideoId { get; set; }

        public string Filters { get; set; }

        public bool Completed { get; set; }

        public int FramesProcessed { get; set; }

        public int FramesCount { get; set; }

        public double Fps { get; set; }
    }
}
