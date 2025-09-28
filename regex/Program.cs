using Regex.Parser;
using Regex.NFA;

using System.Text;
using Regex;
using Regex.Runtime;


// var nfa = regexToNFA.Convert("[a-zA-Z_][a-zA-Z_0-9]*");
// var regexToNFA = RegexToNFA.WithDefaultBuiltinClasses();
// (0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)?
// var nfa = regexToNFA.Convert("(0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)?");
// var nfa = regexToNFA.Convert("([b]+|[a]+)");
// var nfa = regexToNFA.Convert("[^a-z[\\\\\\n\\x98^]");
// var nfa = regexToNFA.Convert("""a|\0|0|\x7f| |\w|\W""");
// var nfa = regexToNFA.Convert("(a+|.+)?[^a]+b");
// var nfa = regexToNFA.Convert("0(x|X)[a-f0-9]+|abc");
// var nfa = regexToNFA.Convert("(a+)?");
// var nfa = regexToNFA.Convert("(0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)z");
// var nfa = regexToNFA.Convert("(((((((((((((((a)+)*)+)*)*)*)*)*)*)*)*)*)*)*)?");
// var nfa = regexToNFA.Convert("(a+)*bz");
// var nfa = regexToNFA.Convert("(a+)?");
// StringBuilder sb = new();
// for (int i = 0; i < 5000; ++i)
//     sb.Append('(');
// sb.Append('a');
// for (int i = 0; i < 5000; ++i)
//     sb.Append(")" + (i % 2 == 0 ? "*" : "+"));
// var nfa = regexToNFA.Convert(sb.ToString());
// nfa = Optimizer.Optimize(nfa);
// Graphviz.RenderAutomaton(Console.Out, nfa);
// Console.WriteLine($"{0xF:X2}");
// var nfa = regexToNFA.Convert("(a+)*bz");
// var scanner = new Scanner(nfa);
// var nfa = regexToNFA.Convert("(a+)?");
// Console.WriteLine(scanner.Match(Encoding.ASCII.GetBytes("\x1e")));
// 
// var nfa = RegexParser.WithDefaultBuiltinClasses().Convert(".(((((((((((((((a)+)*)+)*)*)*)*)*)*)*)*)*)*)*)?");
// nfa = Optimizer.Optimize(nfa);
// Graphviz.RenderAutomaton(Console.Out, nfa);

// var re = new CompiledRegex("(a+)?b");
var re = new CompiledRegex(".+(((((((((((((((a)+)*)+)*)*)*)*)*)*)*)*)*)*)*)?");
Console.WriteLine(re.Match(new ByteArrayReader(Encoding.ASCII.GetBytes("zza"))));
