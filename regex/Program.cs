using Regex;
using Regex.Graphviz;
using Regex.NFA;

using System.Text;

// var nfa = regexToNFA.Convert("(0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)?");
// var nfa = regexToNFA.Convert("[a-zA-Z_][a-zA-Z_0-9]*");
var regexToNFA = new RegexToNFA([]);
var nfa = regexToNFA.Convert("(a+)*bz");
var exec = new Scanner(nfa);
Console.WriteLine(exec.Match(Encoding.ASCII.GetBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaz")));
