using Regex;
using Regex.Graphviz;

var regexToNFA = new RegexToNFA(new List<(char, CharClass)>());
var (s, e) = regexToNFA.Convert("(0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)?");
var nfaToGraphviz = new NFAToGraphviz();
nfaToGraphviz.Convert(Console.Out, s, e);
