using SCI_Lib.Resources.Vocab;

namespace TranslateServer.Documents
{
    public class WordDocument : Document
    {
        public string Project { get; set; }
        public string Usage { get; set; }
        public bool IsTranslate { get; set; }

        public int WordId { get; set; }
        public string Text { get; set; }
    }
}
