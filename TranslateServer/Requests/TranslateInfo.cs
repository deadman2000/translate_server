using System;
using TranslateServer.Model;

namespace TranslateServer.Requests
{
    public class TranslateInfo
    {
        public TranslateInfo(TextTranslate tr)
        {
            Id = tr.Id;
            Author = tr.Author;
            DateCreate = tr.DateCreate;
            Text = tr.Text;
        }

        public string Id { get; set; }
        public string Author { get; }
        public DateTime DateCreate { get; }
        public string Text { get; }
    }

}
