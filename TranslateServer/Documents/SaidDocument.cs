using MongoDB.Bson.Serialization.Attributes;
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
    public string[] Examples { get; set; }

    [BsonIgnoreIfNull]
    public string Prints { get; set; }

    [BsonIgnore]
    public SaidValidation Validation { get; set; }
}
