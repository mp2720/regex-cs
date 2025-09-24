using System.Text;

namespace Regex.NFA
{
    public class GraphvizGenerator
    {
        private readonly HashSet<int> visitedIndices = [];

        private static string PrintEscapedChar(char c)
        {
            return c switch
            {
                '\"' => "\\\\",
                '\r' => "\\\\r",
                '\n' => "\\\\n",
                _ when '\x20' <= c && c <= '\x7e' => c.ToString(),
                _ => $"\\\\x{(int)c:x2}"
            };
        }

        private static string ConvertCharClass(CharClass? cl)
        {
            if (cl == null)
                return "ε";

            var sb = new StringBuilder("[");
            if (cl.Inverted)
                sb.Append('^');

            foreach (var range in cl.Ranges)
            {
                if (range.From == range.To)
                    sb.Append($"{PrintEscapedChar(range.From)}");
                else
                    sb.Append($"{PrintEscapedChar(range.From)}-{PrintEscapedChar(range.To)}");
            }

            sb.Append(']');

            return sb.ToString();
        }

        private void ConvertState(TextWriter w, NFA.State state)
        {
            if (visitedIndices.Contains(state.Index))
                return;
            visitedIndices.Add(state.Index);

            foreach (var trans in state.Transitions)
            {
                NFA.State child = trans.To;
                w.WriteLine($"{state.Index} -> {child.Index} [label=\"{ConvertCharClass(trans.Condition)}\"];");

                if (!visitedIndices.Contains(child.Index))
                    ConvertState(w, trans.To);
            }
        }

        public void Convert(TextWriter w, NFA.Automaton nfa)
        {
            visitedIndices.Clear();
            w.WriteLine("digraph {");
            w.WriteLine($"node [shape=doublecircle]; {nfa.Source.Index} {nfa.Sink.Index};");
            w.WriteLine($"node [shape=circle];");
            w.WriteLine("rankdir=LR;");
            ConvertState(w, nfa.Source);
            w.WriteLine("}");
        }
    }
}
