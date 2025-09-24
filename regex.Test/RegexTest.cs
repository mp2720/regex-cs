namespace regex.Test;

using Regex;
using Regex.Parser;
using Regex.NFA;
using System.Text;

public class UnitTest1
{
    private static Scanner MakeScanner(string regex)
    {
        return new Scanner(RegexToNFA.WithDefaultBuiltinClasses().Convert(regex));
    }

    private static byte[] ToASCII(string s) => Encoding.ASCII.GetBytes(s);

    private static void MatchRegex(string regex, string input)
    {

    }

    [Fact]
    public void TestCharMatch()
    {
        var scan = MakeScanner("""a|\0|0|\x7f| |\w""");
        for (char c = '\x00'; c <= '\xff'; ++c)
        {
            if (c == 'a' || c == '\0' || c == '\x7f' || c == '0' || c == ' ' || c == '_'
                 || '0' <= c && c <= '9' || 'a' <= c && c <= 'z' || 'A' <= c && c <= 'Z')
                Assert.True(scan.Match(ToASCII(c.ToString())));
            else
                Assert.False(scan.Match(ToASCII(c.ToString())));
        }

        scan = MakeScanner("[.^az\\-0-9]");
        for (char c = '\x00'; c <= '\xff'; ++c)
        {
            if (c == '.' || c == '^' || c == 'a' || c == 'z' || c == '-' || '0' <= c && c <= '9')
                Assert.True(scan.Match(ToASCII(c.ToString())));
            else
                Assert.False(scan.Match(ToASCII(c.ToString())));
        }

        scan = MakeScanner("[^a-z[\\\\\\n\\x1f^]");
        for (char c = '\x00'; c <= '\xff'; ++c)
        {
            if ('a' <= c && c <= 'z' || c == '[' || c == '\\' || c == '\n' || c == '\x1f' || c == '^')
                Assert.False(scan.Match(ToASCII(c.ToString())), $"{(int)c}");
            else
                Assert.True(scan.Match(ToASCII(c.ToString())));
        }

        Assert.Throws<InvalidSyntaxException>(() => MakeScanner("[2-1]"));
        Assert.Throws<InvalidSyntaxException>(() => MakeScanner("[]"));
        Assert.Throws<InvalidSyntaxException>(() => MakeScanner("]]"));
        Assert.Throws<InvalidSyntaxException>(() => MakeScanner("["));
        Assert.Throws<InvalidSyntaxException>(() => MakeScanner("[1-]"));
    }
}
