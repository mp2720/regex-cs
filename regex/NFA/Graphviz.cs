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
            if (range.Start == range.End)
                return $"{EscapeChar(range.Start)}";
            else
                return $"{EscapeChar(range.Start)}-{EscapeChar(range.End)}";
        }

        private static string CharClassLabel(CharClass c)
        {
            string inverseSign = c.Inverted ? "^" : "";
            string rangesStr = String.Join("", c.Ranges.Select(r => RangeLabel(r)));
            return $"[{inverseSign}{rangesStr}]";
        }

        private static void RenderState(
            TextWriter w,
            State state,
            BitArray visited,
            BitArray sources)
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

            string fillcolor = "";
            if (state.Back)
                fillcolor = "fillcolor=\"#773333\"";
            else if (sources.Get(state.Index))
                fillcolor = "fillcolor=\"#000000\"";

            string color = "";
            if (state.IsSink)
                color = "color=\"#ff0000\"";

            string margin = "";
            if (!state.IsEpsilon)
                margin = "margin=\"0\"";

            w.WriteLine($"{state.Index} [{label} {shape} {fillcolor} {color} {margin}]");

            foreach (var nextState in state.Next)
            {
                w.WriteLine($"{state.Index} -> {nextState.Index}");
                RenderState(w, nextState, visited, sources);
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

            var sources = new BitArray(nfa.States.Count);
            foreach (var src in nfa.Sources)
                sources.Set(src.Index, true);

            var visited = new BitArray(nfa.States.Count);
            foreach (var src in nfa.Sources)
                RenderState(w, src, visited, sources);

            w.WriteLine("}");
        }
    }
}
