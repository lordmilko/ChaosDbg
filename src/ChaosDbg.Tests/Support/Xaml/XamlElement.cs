using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ChaosDbg.Tests
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class XamlElement : IEnumerable
    {
        private string DebuggerDisplay => ToString();

        public string Name { get; }

        public OrderedDictionary Attributes { get; } = new OrderedDictionary();

        public XamlElement[] Children;

        public XamlElement(string name, XamlElement[] children)
        {
            Name = name;
            Children = children;
        }

        public void Add(string property, object value)
        {
            if (value != null)
                Attributes.Add(property, value);
        }

        public IEnumerator GetEnumerator() => Children.GetEnumerator();

        private XamlTag[] GetLines()
        {
            var lines = new List<XamlTag>();

            var startTagBuilder = new StringBuilder();
            startTagBuilder.Append("<").Append(Name);

            if (Attributes.Count > 0)
                startTagBuilder.Append(" ").Append(string.Join(" ", Attributes.Cast<DictionaryEntry>().Select(v => $"{v.Key}=\"{v.Value}\"")));

            if (Children.Length == 0)
            {
                startTagBuilder.Append(" />");
                return new[] { new XamlTag(startTagBuilder.ToString(), XamlTagKind.SelfClosing) };
            }
            else
                startTagBuilder.Append(">");

            lines.Add(new XamlTag(startTagBuilder.ToString(), XamlTagKind.Start));

            if (Children.Length == 0)
                return lines.ToArray();

            foreach (var child in Children)
                lines.AddRange(child.GetLines());

            lines.Add(new XamlTag($"</{Name}>", XamlTagKind.End));

            return lines.ToArray();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            var lines = GetLines();

            var indentation = -1;

            foreach (var line in lines)
            {
                switch (line.Kind)
                {
                    case XamlTagKind.Start:
                    case XamlTagKind.SelfClosing:
                        indentation++;
                        break;
                    case XamlTagKind.End:
                        indentation--;
                        break;
                }

                for (var i = 0; i < indentation; i++)
                    builder.Append("    ");

                builder.AppendLine(line.Value);

                if (line.Kind == XamlTagKind.SelfClosing)
                    indentation--;
            }

            return builder.ToString();
        }
    }
}
