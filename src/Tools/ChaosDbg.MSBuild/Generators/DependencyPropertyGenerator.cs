using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ChaosDbg.MSBuild
{
    class DependencyPropertyGenerator : Generator<DependencyPropertyInfo>
    {
        private static readonly string[] validAttributes =
        {
            "ChaosDbg.DependencyPropertyAttribute",
            "ChaosDbg.AttachedDependencyPropertyAttribute",
            "DependencyProperty",
            "AttachedDependencyProperty"
        };

        protected override bool AnalyzeAttribute(AttributeSyntax attrib, List<string> errors, List<DependencyPropertyInfo> infos)
        {
            var attribName = attrib.Name.ToString();

            if (!validAttributes.Contains(attribName))
                return false;

            var parentClass = attrib.Parent?.Parent;

            if (!(parentClass is ClassDeclarationSyntax classSyntax))
                return false;

            var result = TransformSyntax(classSyntax, attribName.Contains("Attached"), attrib.ArgumentList?.Arguments.ToArray());

            if (result is DependencyPropertyInfo r)
            {
                infos.Add(r);
                return true;
            }
            else
                errors.Add((string) result);

            return false;
        }

        private object TransformSyntax(ClassDeclarationSyntax classSyntax, bool isAttached, AttributeArgumentSyntax[] attribArgs)
        {
            var name = attribArgs[0].ToString().Trim('"');
            var type = ((TypeOfExpressionSyntax) attribArgs[1].Expression).Type.ToString();
            bool isReadOnly = false;
            string defaultValue = null;
            string defaultValueExpression = null;
            var flags = new List<string>();

            foreach (var arg in attribArgs.Skip(2))
            {
                if (arg.NameEquals == null)
                    throw new NotImplementedException($"Don't know how to handle unnamed argument '{arg.NameEquals}'");

                var property = arg.NameEquals.Name.ToString();

                switch (property)
                {
                    case "IsReadOnly":
                        isReadOnly = arg.Expression.IsKind(SyntaxKind.TrueLiteralExpression);
                        break;

                    case "DefaultValue":
                        defaultValue = arg.Expression.ToString();
                        break;

                    case "DefaultValueExpression":
                        defaultValueExpression = arg.Expression.ToString().Trim('"');
                        break;

                    case "AffectsMeasure":
                    case "AffectsArrange":
                    case "AffectsParentMeasure":
                    case "AffectsParentArrange":
                    case "AffectsRender":
                    case "Inherits":
                    case "OverridesInheritanceBehavior":
                    case "NotDataBindable":
                    case "BindsTwoWayByDefault":
                    case "Journal":
                    case "SubPropertiesDoNotAffectRender":
                        if (arg.Expression.IsKind(SyntaxKind.TrueLiteralExpression))
                            flags.Add(property);
                        break;

                    default:
                        throw new NotImplementedException($"Don't know how to handle property '{property}'");
                }
            }

            if (defaultValue != null && defaultValueExpression != null)
                return $"Cannot specify both 'DefaultValue' and 'DefaultValueExpression' in dependency property {classSyntax}.{name}";

            return new DependencyPropertyInfo(classSyntax.Identifier.Text, name, type, isReadOnly, isAttached, flags.ToArray(), defaultValue ?? defaultValueExpression);
        }

        protected override void GenerateInternal(SyntaxWriter writer, HashSet<string> usings, List<DependencyPropertyInfo> infos)
        {
            var classGroup = infos.GroupBy(v => v.ClassName).ToArray();

            var sortedUsings = SortUsings(usings);

            foreach (var item in sortedUsings)
                writer.WriteLine($"using {item};");

            writer
                .WriteLine()
                .WriteLine("namespace ChaosDbg")
                .WriteLine("{")
                .Indent();

            for (var i = 0; i < classGroup.Length; i++)
            {
                writer
                    .WriteLine($"public partial class {classGroup[i].Key}")
                    .WriteLine("{")
                    .Indent();

                var items = classGroup[i].ToArray();

                for (var j = 0; j < items.Length; j++)
                {
                    var item = items[j];

                    writer.WriteLine($"#region {item.PropertyName}").WriteLine();

                    if (item.IsAttached)
                        GenerateAttached(item, writer);
                    else
                        GenerateNormal(item, writer);

                    writer.WriteLine().WriteLine("#endregion");
                }

                writer
                    .Dedent()
                    .WriteLine("}");

                if (i < classGroup.Length - 1)
                    writer.WriteLine();
            }

            writer
                .Dedent()
                .WriteLine("}");
        }

        private void GenerateNormal(DependencyPropertyInfo item, SyntaxWriter writer)
        {
            GenerateRegistration(item, writer);

            writer.WriteLine();

            if (item.IsReadOnly)
            {
                //DependencyProperty For Key
                writer
                    .WriteLine($"public static readonly DependencyProperty {item.PropertyName}Property = {item.PropertyName}PropertyKey.DependencyProperty;");

                writer.WriteLine();
            }

            //CLR Property
            writer
                .WriteLine($"public {item.PropertyType} {item.PropertyName}")
                .WriteLine("{")
                .Indent()
                .WriteLine($"get => ({item.PropertyType}) GetValue({item.PropertyName}Property);")
                .WriteLine(item.IsReadOnly ?
                    $"private set => SetValue({item.PropertyName}PropertyKey, value);" :
                    $"set => SetValue({item.PropertyName}Property, value);")
                .Dedent()
                .WriteLine("}");
        }

        private void GenerateAttached(DependencyPropertyInfo item, SyntaxWriter writer)
        {
            GenerateRegistration(item, writer);

            writer.WriteLine();

            if (item.IsReadOnly)
            {
                //DependencyProperty For Key
                writer
                    .WriteLine($"public static readonly DependencyProperty {item.PropertyName}Property = {item.PropertyName}PropertyKey.DependencyProperty;");

                writer.WriteLine();
            }

            //Getter Method
            writer
                .WriteLine($"public static {item.PropertyType} Get{item.PropertyName}(UIElement element) => ({item.PropertyType}) element.GetValue({item.PropertyName}Property);");

            writer.WriteLine();

            string setterVisibility;
            string setterDP;

            if (item.IsReadOnly)
            {
                setterVisibility = "private";
                setterDP = $"{item.PropertyName}PropertyKey";
            }
            else
            {
                setterVisibility = "public";
                setterDP = $"{item.PropertyName}Property";
            }

            //Setter method
            writer
                .WriteLine($"{setterVisibility} static void Set{item.PropertyName}(UIElement element, {item.PropertyType} value) => element.SetValue({setterDP}, value);");
        }

        private void GenerateRegistration(DependencyPropertyInfo item, SyntaxWriter writer)
        {
            var registrationInvocation = item.IsReadOnly ?
                $"private static readonly DependencyPropertyKey {item.PropertyName}PropertyKey = DependencyProperty.Register{(item.IsAttached ? "Attached" : string.Empty)}ReadOnly(" :
                $"public static readonly DependencyProperty {item.PropertyName}Property = DependencyProperty.Register{(item.IsAttached ? "Attached" : string.Empty)}(";

            var parameters = new List<string>
            {
                item.IsAttached ? $"name: \"{item.PropertyName}\"" : $"name: nameof({item.PropertyName})",
                $"propertyType: typeof({item.PropertyType})",
                $"ownerType: typeof({item.ClassName})"
            };

            bool hasPropertyMetadata = false;

            string metadataParameterName = item.IsAttached ? "defaultMetadata" : "typeMetadata";

            if (item.NeedMetadata)
            {
                var metadataParameters = new List<string>
                {
                    item.DefaultValue
                };

                if (item.Flags.Length > 0)
                    metadataParameters.Add(string.Join(" | ", item.Flags.Select(v => $"FrameworkPropertyMetadataOptions.{v}")));

                var metadata = $"{metadataParameterName}: new FrameworkPropertyMetadata({string.Join(", ", metadataParameters)})";

                parameters.Add(metadata);

                hasPropertyMetadata = true;
            }

            if (!hasPropertyMetadata && item.IsReadOnly)
                parameters.Add($"{metadataParameterName}: null");

            //DependencyProperty/Key
            writer
                .WriteLine(registrationInvocation)
                .Indent();

            for (var i = 0; i < parameters.Count; i++)
            {
                var str = parameters[i];

                if (i < parameters.Count - 1)
                    str += ",";

                writer.WriteLine(str);
            }

            writer
                .Dedent()
                .WriteLine(");");
        }
    }
}
