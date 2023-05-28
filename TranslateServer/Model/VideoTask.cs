using System;
using System.Text.Json.Serialization;

namespace TranslateServer.Model
{
    public class VideoTask : Document
    {
        public string Type { get; set; }

        public string Project { get; set; }

        public string VideoId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Frame { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Count { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? FrameSkip { get; internal set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[] Frames { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Filters { get; set; }

        [JsonIgnore]
        public DateTime? LastRequest { get; set; }

        [JsonIgnore]
        public bool Completed { get; set; }

        [JsonIgnore]
        public DateTime? DateComplete { get; set; }

        [JsonIgnore]
        public string Runner { get; set; }

        public const string INFO_REQUEST = "INFO_REQUEST";
        public const string GET_TEXT = "GET_TEXT";
        public const string GET_IMAGE = "GET_IMAGE";
    }
}
