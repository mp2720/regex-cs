using System.Text;

namespace Regex.Graphviz
{
    public class NFAToGraphviz
    {
        private HashSet<int> visitedIndices = new HashSet<int>();

        private string ConvertCharClass(CharClass? cl)
        {
            if (cl == null)
                return "ε";

            var sb = new StringBuilder("[");
            if (cl.Inverted)
                sb.Append('^');

            foreach (var range in cl.Ranges)
            {
                if (range.From == range.To)
                    sb.Append($"{range.From}");
                else
                    sb.Append($"{range.From}-{range.To}");
            }

            sb.Append(']');

            return sb.ToString();
        }

        private void ConvertState(TextWriter w, NFAState state)
        {
            if (visitedIndices.Contains(state.Index))
                return;
            visitedIndices.Add(state.Index);

            foreach (var trans in state.Transitions)
            {
                NFAState child = trans.To;
                w.WriteLine($"{state.Index} -> {child.Index} [label=\"{ConvertCharClass(trans.Condition)}\"];");

                if (!visitedIndices.Contains(child.Index))
                    ConvertState(w, trans.To);
            }
        }

        public void Convert(TextWriter w, NFA nfa)
        {
            visitedIndices.Clear();
            w.WriteLine("digraph {");
            w.WriteLine($"node [shape=doublecircle]; {nfa.Start.Index} {nfa.End.Index};");
            w.WriteLine($"node [shape=circle];");
            w.WriteLine("rankdir=LR;");
            ConvertState(w, nfa.Start);
            w.WriteLine("}");
        }
    }
}
