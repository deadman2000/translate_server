using System.Collections.Generic;

namespace TranslateServer.Model;

public class WordValidation
{
    public string Word { get; set; }
    public bool IsValid { get; set; }
    public IEnumerable<string> Ids { get; set; }
}
