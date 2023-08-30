using System.Collections.Generic;

namespace TranslateServer.Model;

public class SaidParsing
{
    public IEnumerable<WordValidation> Words { get; set; }
    public string Error { get; set; }
    public IEnumerable<string> ErrWords { get; set; }
    public bool Match { get; set; }
    public string Tree { get; set; }
}
