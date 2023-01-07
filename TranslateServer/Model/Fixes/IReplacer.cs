
namespace TranslateServer.Model.Fixes
{
    interface IReplacer
    {
        string Description { get; }
        string Replace(string input);
    }
}
