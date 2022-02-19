namespace TranslateServer.Model
{
    public class VideoText : Document
    {
        public string Project { get; set; }

        public string VideoId { get; set; }

        public int Frame { get; set; }

        public string Text { get; set; }

        public int T { get; set; }
    }
}
