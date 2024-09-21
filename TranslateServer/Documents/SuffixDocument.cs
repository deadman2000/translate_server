namespace TranslateServer.Documents
{
    public class SuffixDocument : Document
    {
        public string Project { get; set; }
        public bool IsTranslate { get; set; }

        public string Input { get; set; }
        public int InClass { get; set; }
        public string Output { get; set; }
        public int OutClass { get; set; }
    }
}
