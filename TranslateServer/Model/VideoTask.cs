using System;

namespace TranslateServer.Model
{
    public class VideoTask : Document
    {
        public string Type { get; set; }

        public string Project { get; set; }

        public string VideoId { get; set; }

        public int? Frame { get; set; }

        public int? Count { get; set; }

        public DateTime? LastRequest { get; set; }

        public const string INFO_REQUEST = "INFO_REQUEST";
        public const string GET_TEXT = "GET_TEXT";
        public const string GET_FRAME = "GET_FRAME";
    }
}
