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

        private void DrawArrow(TextWriter w, State src, State dest)
        {
            w.WriteLine($"{src.Index} -> {dest.Index}");
        }

        private void ConvertState(TextWriter w, NFA.State? prev, NFA.State state)
        {
            if (visitedIndices.Contains(state.Index))
                return;
            visitedIndices.Add(state.Index);

            string label;
            if (state.Match == null)
                label = $"label=\"{state.Index}\"";
            else
                label = $"label=\"{state.Index} {ConvertCharClass(state.Match)}\"";

            string shape;
            if (state.IsEpsilon)
                shape = "shape=\"circle\"";
            else
                shape = "shape=\"doublecircle\"";

            string color = "";
            if (prev == null || state.Next1 == null && state.Next2 == null)
                // source and sink states
                color = "fillcolor=\"#111111\"";
            else if (state.Back)
                color = "fillcolor=\"#773333\"";

            w.WriteLine($"{state.Index} [{label} {shape} {color}]");

            if (state.Next1 != null)
            {
                DrawArrow(w, state, state.Next1);
                ConvertState(w, state, state.Next1);
            }
            if (state.Next2 != null)
            {
                DrawArrow(w, state, state.Next2);
                ConvertState(w, state, state.Next2);
            }
        }

        public void Convert(TextWriter w, NFA.Automaton nfa)
        {
            w.WriteLine("""
            digraph G {
            bgcolor="#181818";

            node [
              fontcolor = "#e6e6e6",
              style = filled,
              color = "#e6e6e6",
              fillcolor = "#333333"
            ]

            edge [
              color = "#e6e6e6",
              fontcolor = "#e6e6e6"
            ]
            """);
            w.WriteLine($"node [shape=circle];");
            w.WriteLine("rankdir=LR;");

            visitedIndices.Clear();
            foreach (var src in nfa.Sources)
                ConvertState(w, null, src);

            w.WriteLine("}");
        }
    }
}
