using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

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
    }
}
