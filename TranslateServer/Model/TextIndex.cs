using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TranslateServer.Model
{
    public class TextIndex
    {
        public string Project { get; set; }
        
        public string Text { get; set; }

        public string Link { get; set; }

        public bool Translate { get; set; }
    }
}
