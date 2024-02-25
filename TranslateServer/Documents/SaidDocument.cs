using MongoDB.Bson.Serialization.Attributes;
using System;
using TranslateServer.Model;

namespace TranslateServer.Documents;

public class SaidDocument : Document
{
    public string Project { get; set; }

    public int Script { get; set; }

    public int Index { get; set; }

    public string Expression { get; set; }

    [BsonIgnoreIfNull]
    public string Patch { get; set; }

    public bool Approved { get; set; }

    [BsonIgnoreIfNull]
    [Obsolete("Use `Tests` instead")]
    public string[] Examples { get; set; }

    [BsonIgnoreIfNull]
    public SaidTest[] Tests { get; set; }

    [BsonIgnoreIfNull]
    public string Prints { get; set; }

    public bool? IsValid { get; set; }

    [BsonIgnore]
    public SaidValidation Validation { get; set; }
}
