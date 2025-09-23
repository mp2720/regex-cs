using Regex;
using Regex.Graphviz;

var regexToNFA = new RegexToNFA(new List<(char, CharClass)>());
var nfa = regexToNFA.Convert("(0|0x[0-9a-fA-f]+|[1-9][0-9]*|0o[0-7]+|0b[01]+)?");
new NFAToGraphviz().Convert(Console.Out, nfa);
