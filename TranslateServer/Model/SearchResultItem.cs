namespace TranslateServer.Model
{
    public class SearchResultItem
    {
        public string Id { get; set; }

        public string Project { get; set; }

        public string Volume { get; set; }

        public int Number { get; set; }

        public string Html { get; set; }

        public double? Score { get; set; }

        public string Text { get; internal set; }
    }
}
