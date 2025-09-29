[assembly: CaptureConsole]

namespace regex.Test;

using Regex;
using Regex.Parser;


public class UnitTest1
{
    [Fact]
    public void TestCharMatch()
    {
        var re = new CompiledRegex("""a|\0|0|\x7f| |_|\w""");
        for (char c = '\x00'; c <= '\xff'; ++c)
        {
            if (c == 'a' || c == '\0' || c == '\x7f' || c == '0' || c == ' ' || c == '_'
                 || '0' <= c && c <= '9' || 'a' <= c && c <= 'z' || 'A' <= c && c <= 'Z')
                Assert.True(re.Match([(byte)c]), $"{(int)c:x}");
            else
                Assert.False(re.Match([(byte)c]));
        }

        re = new CompiledRegex("[.^az\\-0-9]");
        for (char c = '\x00'; c <= '\xff'; ++c)
        {
            if (c == '.' || c == '^' || c == 'a' || c == 'z' || c == '-' || '0' <= c && c <= '9')
                Assert.True(re.Match([(byte)c]));
            else
                Assert.False(re.Match([(byte)c]));
        }

        re = new CompiledRegex("[^a-z[\\\\\\n\\x9f^]");
        for (char c = '\x00'; c <= '\xff'; ++c)
        {
            if ('a' <= c && c <= 'z' || c == '[' || c == '\\' || c == '\n' || c == '\x9f' || c == '^')
                Assert.False(re.Match([(byte)c]));
            else
                Assert.True(re.Match([(byte)c]));
        }
    }

    [Fact]
    public void TestInvalidSyntax()
    {
        Assert.Throws<ParsingException>(() => new CompiledRegex("[2-1]"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("[]"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("]]"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("["));
        Assert.Throws<ParsingException>(() => new CompiledRegex("[1-]"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("a|"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("|b"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("a++"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("a+?"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("a*+"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("(a"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("a)"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("(()"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("()"));
        Assert.Throws<ParsingException>(() => new CompiledRegex("a+\\!junk!!!!"));
    }

    /// <summary>
    /// Run tests on all words from S* language (S = alphabet).
    /// Switching reuseCompiled flag help find more bugs in runtime code.
    /// This method also tests code for bugs caused by passing unpinned or GCed addresses to runtime,
    /// since it repeatedly allocates and throws away a lot of arrays for caching S* words generation.
    /// </summary>
    private void CompareWithDotNETRegexOnKleeneClosure(
        string re,
        bool reuseCompiled,
        string dotNetRe,
        IEnumerable<char> alphabet,
        int maxWordLen)
    {
        var cre = new CompiledRegex(re);

        var dotNetCRe = new System.Text.RegularExpressions.Regex(dotNetRe);

        var lang = new AlphabetKleeneClosure(alphabet.Select(c => (byte)c), maxWordLen);
        foreach (var w in lang.Words())
        {
            if (!reuseCompiled)
                cre = new CompiledRegex(re);


            var ws = System.Text.Encoding.UTF8.GetString(w);

            var dotNetMatch = dotNetCRe.Match(ws);
            Assert.True(dotNetMatch.Success == cre.Match(w), $"'{re}' != '{dotNetRe}' on '{ws}'");
        }
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 12)]
    public void TestOnKleeneClosure1(bool reuseCompiled, int maxWordLen)
    {
        CompareWithDotNETRegexOnKleeneClosure(@"[^a1]|a*", reuseCompiled, @"^([^a1]|a*)$", ['a', '1', ' '], maxWordLen);
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 12)]
    public void TestOnKleeneClosure2(bool reuseCompiled, int maxWordLen)
    {
        CompareWithDotNETRegexOnKleeneClosure(
            @"[01]+1[01][01]", reuseCompiled,
            @"^([01]+1[01][01])$", ['0', '1', 'z'],
            maxWordLen
        );
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 11)]
    public void TestOnKleeneClosure3(bool reuseCompiled, int maxWordLen)
    {
        CompareWithDotNETRegexOnKleeneClosure(
            @"(a|bc)+z", reuseCompiled,
            @"^(a|bc)+z$", ['a', 'b', 'c'],
            maxWordLen
        );
    }
}
