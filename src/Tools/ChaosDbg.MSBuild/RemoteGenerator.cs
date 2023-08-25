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
            var usings = new HashSet<string>();
            var infos = new List<ReactiveCommandInfo>();

            foreach (var file in files)
            {
                var contents = File.ReadAllText(file);

                var tree = CSharpSyntaxTree.ParseText(contents);

                var attribs = tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>();

                bool include = false;

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
                    {
                        infos.Add(r);
                        include = true;
                    }
                    else
                        errors.Add((string)result);
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

            var groups = infos.GroupBy(v => v.ClassName).ToArray();

            usings.Add("ChaosDbg.Reactive");

            var sortedUsings = SortUsings(usings);

            foreach (var item in sortedUsings)
                writer.WriteLine($"using {item};");

            writer
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

                    string canExecuteArg = null;

                    if (item.CanExecuteName != null)
                    {
                        var value = item.CanExecuteName;

                        if (item.NeedCanExecuteFuncWrapper)
                            value = $"_ => {item.CanExecuteName}()";

                        canExecuteArg = $", {value}";
                    }

                    //Property
                    writer
                        .WriteLine("/// <summary>")
                        .WriteLine($"/// Gets an <see cref=\"{item.XmlCommandType}\"/> instance wrapping <see cref=\"{item.MethodName}\"/>")
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
            var classSyntax = (ClassDeclarationSyntax)method.Parent;

            var className = classSyntax.Identifier.Text;

            var namespaceSyntax = method.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            if (namespaceSyntax == null)
                return $"Method '{className}.{method.Identifier.Text}' is not contained in a namespace";

            var ns = namespaceSyntax.Name.ToString();

            if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
                return $"Type '{className}' must be marked as partial to generate RelayCommand for method '{method.Identifier.Text}'";

            var error = TryGetFieldAndPropertyName(method, out var propertyName, out var fieldName);

            if (error != null)
                return error;

            error = TryGetParameterType(method, out var parameterType);

            if (error != null)
                return error;

            var canExecute = GetCanExecute(attribArgs);

            bool needCanExecuteFuncWrapper = false;

            if (canExecute != null)
                error = TryValidateCanExecute(canExecute, method, parameterType, out needCanExecuteFuncWrapper);

            if (error != null)
                return error;

            var info = new ReactiveCommandInfo(
                ns,
                className,
                method.Identifier.Text,
                propertyName,
                fieldName,
                canExecute,
                parameterType,
                needCanExecuteFuncWrapper
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

        private string TryGetFieldAndPropertyName(MethodDeclarationSyntax method, out string propertyName, out string fieldName)
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

        private string TryGetParameterType(MethodDeclarationSyntax method, out string parameterType)
        {
            var parameters = method.ParameterList.Parameters;

            if (parameters.Count == 0)
            {
                parameterType = null;
                return null;
            }

            if (parameters.Count > 1)
            {
                parameterType = null;
                return $"Cannot generate RelayCommand<T> for method {((ClassDeclarationSyntax)method.Parent).Identifier.Text}.{method.Identifier.Text}: method has multiple parameters.";
            }

            parameterType = parameters[0].Type?.ToString();
            return null;
        }

        private string TryValidateCanExecute(string canExecuteMethodName, MethodDeclarationSyntax executeMethod, string parameterType, out bool needWrapperFunc)
        {
            var classSyntax = (ClassDeclarationSyntax) executeMethod.Parent;
            needWrapperFunc = false;

            var candidates = classSyntax.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.Text == canExecuteMethodName).ToArray();

            //no canexecute method found at all
            if (candidates.Length == 0)
                return $"No method named '{canExecuteMethodName}' in class '{classSyntax.Identifier.Text}' was found";

            if (parameterType == null)
            {
                foreach (var candidate in candidates)
                {
                    if (candidate.ParameterList.Parameters.Count == 0)
                        return null;
                }

                return $"Cannot generate RelayCommand: expected method '{classSyntax.Identifier.Text}.{canExecuteMethodName}' to not contain any parameters. Either add a parameter to method '{executeMethod.Identifier.Text}' or remove the parameters from method '{canExecuteMethodName}'.";
            }
            else
            {
                if (candidates.Length == 1)
                {
                    var parameters = candidates[0].ParameterList.Parameters;

                    if (parameters.Count == 0)
                    {
                        needWrapperFunc = true;
                        return null;
                    }

                    if (parameters.Count > 1)
                        return $"Cannot generate RelayCommand<T>: method '{classSyntax.Identifier.Text}.{canExecuteMethodName}' contains multiple parameters.";

                    if (parameters[0].Type?.ToString() != parameterType)
                        return $"Cannot generate RelayCommand<T>: expected method '{classSyntax.Identifier.Text}.{canExecuteMethodName}' to contain a parameter of type '{parameterType}'.";

                    //Success!
                    return null;
                }
                else
                {
                    foreach (var candidate in candidates)
                    {
                        if (candidate.ParameterList.Parameters.Count == 1)
                        {
                            var foundType = candidate.ParameterList.Parameters[0].Type?.ToString();

                            //Success!
                            if (foundType == parameterType)
                                return null;
                        }
                    }

                    //No matches found
                    return $"Cannot generate RelayCommand<T>: could not find a '{canExecuteMethodName}' method that takes a single parameter of type '{parameterType}'.";
                }
            }
        }

        private string[] SortUsings(HashSet<string> usings)
        {
            var system = usings.Where(v => v.StartsWith("System")).OrderBy(v => v).ToArray();
            var nonSystem = usings.Except(system).OrderBy(v => v);

            return system.Concat(nonSystem).ToArray();
        }

        public override object InitializeLifetimeService() => null;
    }
}
