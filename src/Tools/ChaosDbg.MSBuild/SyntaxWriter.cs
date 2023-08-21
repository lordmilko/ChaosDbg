using System.Text;

namespace ChaosDbg.MSBuild
{
    //The files we generate are going to become part of our project. As such, we can't use Roslyn to generate these,
    //as its syntax formatting looks yucky
    class SyntaxWriter
    {
        private StringBuilder builder = new StringBuilder();

        private int level;

        public SyntaxWriter WriteLine(string value)
        {
            WriteLevel();
            builder.AppendLine(value);
            return this;
        }

        public SyntaxWriter WriteLine()
        {
            builder.AppendLine();
            return this;
        }

        public SyntaxWriter Indent()
        {
            level++;
            return this;
        }

        public SyntaxWriter Dedent()
        {
            level--;
            return this;
        }

        private void WriteLevel()
        {
            for (var i = 0; i < level; i++)
                builder.Append("    ");
        }

        public override string ToString()
        {
            return builder.ToString();
        }
    }
}
