namespace TranslateServer.Model.Fixes
{
    class TrimEnd : IReplacer
    {
        private readonly string _description;
        private readonly char[] _chars;

        public TrimEnd(string description)
        {
            _description = description;
            _chars = null;
        }

        public TrimEnd(string description, params char[] chars)
        {
            _description = description;
            _chars = chars;
        }
        public string Description => _description;

        public string Replace(string input)
        {
            if (_chars != null)
                return input.TrimEnd(_chars);
            return input.TrimEnd();
        }
    }
}
