using Regex.Parser;
using Regex.NFA;
using System.Text;
using Regex;

string re;
// re = "(0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)?";
// re = "([b]+|[a]+)";
// re = "[^a-z[\\\\\\n\\x98^]";
// re = """a|\0|0|\x7f| |\w|\W""";
// re = "(a+|.+)?[^a]+b";
// re = "0(x|X)[a-f0-9]+|abc";
// re = "(a+)?";
// re = "(0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)z";
// re = "(((((((((((((((a)+)*)+)*)*)*)*)*)*)*)*)*)*)*)?";
// re = "(a+)*bz";
// re = "(a+)?";
// re = "v(a|b*|c)z";
// re = "[01]+1[01][01][01][01][01]";
// re = "[0-9a-zA-Z_]|a|\0|0|\x7f| |_";
// re = "a|[0-9a-zA-Z_]";
re = @"[^a1]|a*";

var input = "a111a1";

var nfa = RegexParser.WithDefaultBuiltinClasses().Convert(re);
nfa = Optimizer.Optimize(nfa);

using (var writer = new StreamWriter("/tmp/regex-cs-nfa.dot"))
{
    Graphviz.RenderAutomaton(writer, nfa);
}

var cre = new CompiledRegex(re);
Console.Error.WriteLine(cre.Match(Encoding.ASCII.GetBytes(input)));
