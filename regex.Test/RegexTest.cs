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

    private void CompareWithDotNETRegex(
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
            // Console.WriteLine(ws);

            var dotNetMatch = dotNetCRe.Match(ws);
            Assert.True(dotNetMatch.Success == cre.Match(w), $"'{re}' != '{dotNetRe}' on '{ws}'");
        }
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 12)]
    public void TestOnKleeneClosure1(bool reuseCompiled, int maxWordLen)
    {
        CompareWithDotNETRegex(@"[^a1]|a*", reuseCompiled, @"^([^a1]|a*)$", ['a', '1', ' '], maxWordLen);
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 12)]
    public void TestOnKleeneClosure2(bool reuseCompiled, int maxWordLen)
    {
        CompareWithDotNETRegex(
            @"[01]+1[01][01]", reuseCompiled,
            @"^([01]+1[01][01])$", ['a', '1', ' '],
            maxWordLen
        );
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 12)]
    public void TestOnKleeneClosure3(bool reuseCompiled, int maxWordLen)
    {
        CompareWithDotNETRegex(
            @"[01]+1[01][01]", reuseCompiled,
            @"^([01]+1[01][01])$", ['a', '1', ' '],
            maxWordLen
        );
    }

    // [Fact]
    // public void Test1()
    // {
    //     var cre = new CompiledRegex("[01]+1[01][01]");
    //     cre = new CompiledRegex("([^a1]|a*)");
    //     for (int i = 0; i < 10000; ++i)
    //     {
    //         var rnd = new Random();
    //         long n = rnd.NextInt64(0, 100);
    //         byte[] b = new byte[n];
    //         rnd.NextBytes(b);
    //         cre.Match(b);
    //     }
    // }

    // [Fact]
    // public void Test2()
    // {
    //     var cre = new CompiledRegex("[01]+1[01][01]");
    //     cre = new CompiledRegex("([^a1]|a*)");
    //     for (int i = 0; i < 10000; ++i)
    //     {
    //         var rnd = new Random();
    //         long n = rnd.NextInt64(0, 100);
    //         byte[] b = new byte[n];
    //         rnd.NextBytes(b);
    //         cre.Match(b);
    //     }
    // }

    // [Fact]
    // public void Test3()
    // {

    //     CompareWithDotNETRegex(@"[^a1]|a*", false, @"^([^a1]|a*)$", ['a', '1', ' '], 10);
    // }
    // [Fact]
    // public void Test4()
    // {

    //     CompareWithDotNETRegex(@"[^a1]|a*", false, @"^([^a1]|a*)$", ['a', '1', ' '], 10);
    // }
    // [Fact]
    // public void Test5()
    // {

    //     CompareWithDotNETRegex(@"[^a1]|a*", false, @"^([^a1]|a*)$", ['a', '1', ' '], 10);
    // }
}
