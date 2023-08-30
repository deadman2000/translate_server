namespace TranslateServer.Documents
{
    public class SynonymDocument : Document
    {
        public string Project { get; set; }

        public int Script { get; set; }

        public int? Index { get; set; }

        public ushort WordA { get; set; }

        public ushort WordB { get; set; }

        public bool Delete { get; set; }
    }
}
