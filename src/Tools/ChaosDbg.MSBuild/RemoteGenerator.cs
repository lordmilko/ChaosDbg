using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ChaosDbg.MSBuild
{
    /// <summary>
    /// Generates source generated files in a separate <see cref="AppDomain"/>.
    /// </summary>
    class RemoteGenerator : MarshalByRefObject
    {
        public string[] ExecuteRemote(string[] files, string output)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            return ExecuteRemoteInternal(files, output);
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = args.Name;

            if (args.Name != null && args.Name.Contains(","))
                name = name.Substring(0, name.IndexOf(','));

            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(GetType().Assembly.Location), "*.dll"))
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(file), name))
                    return Assembly.LoadFile(file);
            }

            return null;
        }

        private string[] ExecuteRemoteInternal(string[] files, string output)
        {
            var errors = new List<string>();
            var infos = new List<ReactiveCommandInfo>();

            foreach (var file in files)
            {
                var contents = File.ReadAllText(file);

                var tree = CSharpSyntaxTree.ParseText(contents);

                var attribs = tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>();

                foreach (var attrib in attribs)
                {
                    var attribName = attrib.Name.ToString();

                    if (attribName != "ChaosDbg.Reactive.RelayCommandAttribute" && attribName != "RelayCommand")
                        continue;

                    var parentMethod = attrib.Parent?.Parent;

                    if (!(parentMethod is MethodDeclarationSyntax methodSyntax))
                        continue;

                    var parentClass = parentMethod.Parent;

                    if (!(parentClass is ClassDeclarationSyntax))
                        continue;

                    var result = TransformSyntax(methodSyntax, attrib.ArgumentList?.Arguments.ToArray());

                    if (result is ReactiveCommandInfo r)
                        infos.Add(r);
                    else
                        errors.Add((string)result);
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

            var groups = infos.GroupBy(v => v.ClassName).ToArray();

            writer
                .WriteLine("using ChaosDbg.Reactive;")
                .WriteLine()
                .WriteLine("namespace ChaosDbg.ViewModel")
                .WriteLine("{")
                .Indent();

            for (var i = 0; i < groups.Length; i++)
            {
                writer
                    .WriteLine($"public partial class {groups[i].Key}")
                    .WriteLine("{")
                    .Indent();

                var items = groups[i].ToArray();

                for (var j = 0; j < items.Length; j++)
                {
                    var item = items[j];

                    //Field
                    writer
                        .WriteLine("/// <summary>")
                        .WriteLine($"/// The backing field for <see cref=\"{item.PropertyName}\"/>")
                        .WriteLine("/// </summary>")
                        .WriteLine($"private {item.PropertyType} {item.FieldName};")
                        .WriteLine();

                    var canExecuteArg = item.CanExecuteName != null ? $", {item.CanExecuteName}" : null;

                    //Property
                    writer
                        .WriteLine("/// <summary>")
                        .WriteLine($"/// Gets an <see cref=\"{item.PropertyType}\"/> instance wrapping <see cref=\"{item.MethodName}\"/>")
                        .WriteLine("/// </summary>")
                        .WriteLine($"public {item.PropertyType} {item.PropertyName}")
                        .WriteLine("{").Indent()
                            .WriteLine("get")
                            .WriteLine("{").Indent()
                                .WriteLine($"if ({item.FieldName} == null)")
                                .Indent()
                                    .WriteLine($"{item.FieldName} = new {item.CommandType}({item.MethodName}{canExecuteArg});")
                                .Dedent()
                                .WriteLine()
                                .WriteLine($"return {item.FieldName};")
                            .Dedent().WriteLine("}");

                    writer
                        .Dedent()
                        .WriteLine("}");

                    if (j < items.Length - 1)
                        writer.WriteLine();
                }

                writer
                    .Dedent()
                    .WriteLine("}");

                if (i < groups.Length - 1)
                    writer.WriteLine();
            }

            writer
                .Dedent()
                .WriteLine("}");

            var str = writer.ToString();

            File.WriteAllText(output, str);

            return new string[0];
        }

        private object TransformSyntax(MethodDeclarationSyntax method, AttributeArgumentSyntax[] attribArgs)
        {
            var diagnostics = new List<Diagnostic>();

            var classSyntax = (ClassDeclarationSyntax)method.Parent;

            var className = classSyntax.Identifier.Text;

            var namespaceSyntax = method.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            if (namespaceSyntax == null)
                return $"Method '{className}.{method.Identifier.Text}' is not contained in a namespace";

            var ns = namespaceSyntax.Name.ToString();

            if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                return $"Type '{className}' must be marked as partial to generate RelayCommand for method '{method.Identifier.Text}'";

            var error = TryGetFieldAndPropertyName(method, diagnostics, out var propertyName, out var fieldName);

            if (error != null)
                return error;

            var canExecute = GetCanExecute(attribArgs);

            var info = new ReactiveCommandInfo(
                ns,
                className,
                method.Identifier.Text,
                propertyName,
                fieldName,
                canExecute
            );

            return info;
        }

        private string GetCanExecute(AttributeArgumentSyntax[] attribArgs)
        {
            if (attribArgs == null)
                return null;

            foreach (var arg in attribArgs)
            {
                var name = arg.NameEquals?.Name.ToString();

                if (name == "CanExecute")
                {
                    var value = arg.Expression;

                    if (value is InvocationExpressionSyntax i)
                    {
                        //Implicitly a nameof expression
                        return i.ArgumentList.Arguments[0].ToString();
                    }
                    else
                        return value.ToString().Trim('"');
                }
            }

            return null;
        }

        private string TryGetFieldAndPropertyName(MethodDeclarationSyntax method, List<Diagnostic> diagnostics, out string propertyName, out string fieldName)
        {
            var methodName = method.Identifier.Text;

            if (!methodName.StartsWith("On") || methodName.Length < 3 || !char.IsUpper(methodName[2]))
            {
                propertyName = null;
                fieldName = null;
                return $"Cannot generate RelayCommand for method {((ClassDeclarationSyntax) method.Parent).Identifier.Text}.{method.Identifier.Text}: method must start with 'On' followed by another word starting with an uppercase letter.";
            }

            propertyName = methodName.Substring(2) + "Command";
            fieldName = char.ToLower(propertyName[0]) + propertyName.Substring(1);
            return null;
        }

        public override object InitializeLifetimeService() => null;
    }
}
