using System.Collections.Generic;

namespace TranslateServer.Model.Yandex
{
    public class SpellResult
    {
        public int Code { get; set; }
        public int Pos { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public int Len { get; set; }
        public string Word { get; set; }
        public IEnumerable<string> S { get; set; }
    }
}
