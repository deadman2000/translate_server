using System.Collections.Generic;

namespace TranslateServer.Model.Import
{
    public class ImportVolume
    {
        public string Name { get; set; }
        public List<ImportTranslate> Translates { get; set; }
    }
}
