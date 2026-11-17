using System.Reflection;
using System.Text.RegularExpressions;
using TranslateServer.Store;
using Xunit;

namespace TranslateServer.Tests;

public class PatchRuBracketsTests
{
    private static readonly MethodInfo PatchMethod = typeof(TranslateStore).GetMethod(
        "PatchRuBrackets",
        BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string Patch(string txt) => (string)PatchMethod.Invoke(null, [txt])!;

    [Fact]
    public void PatchesEveryParentheticalInSample()
    {
        const string input = "|c1|Другие законы касались навигации (ограничения торговых путей), патоки (высокий налог на патоку), и сахара (высокий налог на сахар).|c|";

        var actual = Patch(input);

        Assert.StartsWith("|c1|Другие законы касались навигации (", actual);
        Assert.EndsWith(").|c|", actual);
        Assert.Contains("), патоки (", actual);
        Assert.Contains("), и сахара (", actual);

        Assert.Contains("(\u006f\u0433\u0440\u0061\u043d\u0438\u0447\u0065\u043d\u0438\u044f \u0442\u006f\u0440\u0433\u006f\u0432\u044b\u0078 \u043f\u0443\u0442\u0065\u0439)", actual);
        Assert.Contains("(\u0432\u044b\u0063\u006f\u043a\u0438\u0439 \u043d\u0061\u043b\u006f\u0433 \u043d\u0061 \u043f\u0061\u0442\u006f\u043a\u0443)", actual);
        Assert.Contains("(\u0432\u044b\u0063\u006f\u043a\u0438\u0439 \u043d\u0061\u043b\u006f\u0433 \u043d\u0061 \u0063\u0061\u0078\u0061\u0440)", actual);
        Assert.Equal(3, Regex.Matches(actual, @"\([^)]*\)").Count);
    }

    [Fact]
    public void PatchesLaterBracketWhenFirstHasNoLookalikes()
    {
        const string input = "(или) и (окно)";

        var actual = Patch(input);

        Assert.StartsWith("(или) и (", actual);
        Assert.Contains("(\u006f\u043a\u043d\u006f)", actual);
    }

    [Fact]
    public void LeavesTextWithoutBracketsAndSafeBrackets()
    {
        Assert.Equal("сахар без скобок", Patch("сахар без скобок"));
        Assert.Equal("уже (test) конец", Patch("уже (test) конец"));
    }
}
