using System;
using System.Collections.Generic;
using TranslateServer.Documents;
using TranslateServer.Model.Yandex;

namespace TranslateServer.Requests
{
    public class TranslateInfo
    {
        public TranslateInfo(TextTranslate tr, IEnumerable<Comment> comments = null)
        {
            Id = tr.Id;
            Author = tr.Author;
            Editor = tr.Editor;
            DateCreate = tr.DateCreate;
            Text = tr.Text;
            Spellcheck = tr.Spellcheck;
            Comments = comments;
        }

        public string Id { get; set; }
        public string Author { get; }
        public string Editor { get; }
        public DateTime DateCreate { get; }
        public string Text { get; }
        public SpellResult[] Spellcheck { get; }
        public IEnumerable<Comment> Comments { get; set; }
    }

}
