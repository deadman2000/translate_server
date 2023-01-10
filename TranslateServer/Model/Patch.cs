using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.IO;

namespace TranslateServer.Model
{
    public class Patch : Document
    {
        public string Project { get; set; }
        
        public string FileName { get; internal set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string FileId { get; set; }

        public string User { get; set; }

        public DateTime UploadDate { get; set; }

        public bool Deleted { get; set; }

        public string Extension => Path.GetExtension(FileName);

        public int Number => int.TryParse(FileName.Split('.')[0], out var n) ? n : -1;
    }
}
