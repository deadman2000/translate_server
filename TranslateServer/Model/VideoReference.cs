namespace TranslateServer.Model
{
    public class VideoReference : Document
    {
        public string Project { get; set; }

        public string Volume { get; set; }

        public int Number { get; set; }

        public string VideoId { get; set; }

        public int Frame { get; set; }

        public int T { get; set; }

        public double? Score { get; set; }

        public double Rate { get; set; }
    }
}