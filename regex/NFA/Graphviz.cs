using System.Collections;

namespace Regex.NFA
{
    public static class Graphviz
    {
        private static string EscapeChar(char c) =>
            c switch
            {
                '\"' => "\\\\",
                '\r' => "\\\\r",
                '\n' => "\\\\n",
                _ when '\x20' <= c && c <= '\x7e' => c.ToString(),
                _ => $"\\\\x{(int)c:x2}"
            };

        private static string RangeLabel(CharRange range)
        {
            if (range.start == range.end)
                return $"{EscapeChar(range.start)}";
            else
                return $"{EscapeChar(range.start)}-{EscapeChar(range.end)}";
        }

        private static string CharClassLabel(CharClass c)
        {
            string inverseSign = c.inverted ? "^" : "";
            string rangesStr = String.Join("", c.ranges.Select(r => RangeLabel(r)));
            return $"[{inverseSign}{rangesStr}]";
        }

        private static void RenderState(TextWriter w, bool isSource, State state, BitArray visited)
        {
            if (visited.Get(state.Index))
                return;
            visited.Set(state.Index, true);

            string label;
            if (state.Condition == null)
                label = $"label=\"{state.Index}\"";
            else
                label = $"label=\"{state.Index} {CharClassLabel(state.Condition)}\"";

            string shape;
            if (state.IsEpsilon)
                shape = "shape=\"circle\"";
            else
                shape = "shape=\"doublecircle\"";

            string color = "";
            if (state.Back)
                color = "fillcolor=\"#773333\"";
            else if (isSource || state.IsSink)
                color = "fillcolor=\"#111111\"";

            w.WriteLine($"{state.Index} [{label} {shape} {color}]");

            foreach (var nextState in state.Next)
            {
                w.WriteLine($"{state.Index} -> {nextState.Index}");
                RenderState(w, false, nextState, visited);
            }
        }

        public static void RenderAutomaton(TextWriter w, Automaton nfa)
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
            rankdir=LR;
            """);

            var visited = new BitArray(nfa.states.Count);
            foreach (var src in nfa.sources)
                RenderState(w, true, src, visited);

            w.WriteLine("}");
        }
    }
}
