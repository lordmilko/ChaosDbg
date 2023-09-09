using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ChaosDbg.MSBuild
{
    abstract class Generator<T>
    {
        public string[] Generate(string[] files, string output)
        {
            var errors = new List<string>();
            var usings = new HashSet<string>();
            var infos = new List<T>();

            foreach (var file in files)
            {
                var contents = File.ReadAllText(file);

                var tree = CSharpSyntaxTree.ParseText(contents);

                var attribs = tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>();

                bool include = false;

                foreach (var attrib in attribs)
                {
                    include |= AnalyzeAttribute(attrib, errors, infos);
                }

                if (include)
                {
                    foreach (var item in tree.GetCompilationUnitRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().Select(u => u.Name.ToString()))
                        usings.Add(item);
                }
            }

            if (errors.Count > 0)
                return errors.ToArray();

            if (infos.Count == 0)
            {
                if (File.Exists(output))
                    File.Delete(output);

                return Array.Empty<string>();
            }

            var writer = new SyntaxWriter();

            GenerateInternal(writer, usings, infos);

            var str = writer.ToString();

            File.WriteAllText(output, str);

            return new string[0];
        }

        protected abstract bool AnalyzeAttribute(AttributeSyntax attrib, List<string> errors, List<T> infos);

        protected abstract void GenerateInternal(SyntaxWriter writer, HashSet<string> usings, List<T> infos);

        protected string[] SortUsings(HashSet<string> usings)
        {
            var system = usings.Where(v => v.StartsWith("System")).OrderBy(v => v).ToArray();
            var nonSystem = usings.Except(system).OrderBy(v => v);

            return system.Concat(nonSystem).ToArray();
        }
    }
}
