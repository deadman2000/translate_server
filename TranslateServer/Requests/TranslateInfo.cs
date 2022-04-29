using System;
using System.Collections.Generic;
using TranslateServer.Model;

namespace TranslateServer.Requests
{
    public class TranslateInfo
    {
        public TranslateInfo(TextTranslate tr, IEnumerable<Comment> comments = null)
        {
            Id = tr.Id;
            Author = tr.Author;
            DateCreate = tr.DateCreate;
            Text = tr.Text;
            Comments = comments;
        }

        public string Id { get; set; }
        public string Author { get; }
        public DateTime DateCreate { get; }
        public string Text { get; }

        public IEnumerable<Comment> Comments { get; set; }
    }

}
