using System.Text.RegularExpressions;

namespace TranslateServer.Model.Fixes
{
    class RegexReplace : IReplacer
    {
        private readonly string _description;
        private readonly Regex _reg;
        private readonly string _replace;

        public RegexReplace(string description, string pattern, string replace)
        {
            _description = description;
            _reg = new Regex(pattern, RegexOptions.Compiled | RegexOptions.Multiline);
            _replace = replace;
        }

        public string Description => _description;

        public string Replace(string input) => _reg.Replace(input, _replace);
    }
}
