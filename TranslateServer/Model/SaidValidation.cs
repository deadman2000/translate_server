using System.Collections.Generic;

namespace TranslateServer.Model;

public class SaidValidation
{
    public string Error { get; set; }
    public string ErrWord { get; set; }
    public IEnumerable<string> Said { get; set; }
    public string SaidTree { get; set; }
    public List<SaidParsing> Tests { get; set; }
    public bool Valid { get; set; }
}
