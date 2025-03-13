using Microsoft.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Sun.SourceGenerator.Attributes;

namespace Sun.SourceGenerator.Generator
{
    [Generator]
    public class CacheGenerator : ISourceGenerator
    {
        private readonly Dictionary<string, string> _type2Name = new()
        {
            ["GameObject"] = "gameObject",
        };

        public void Initialize(GeneratorInitializationContext context)
        {
            // 注册一个接收语法节点的回调
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;
            var classDeclarations = receiver.ClassDeclarations;

            foreach (var classDeclaration in classDeclarations)
            {
                var classSymbol = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree)
                    .GetDeclaredSymbol(classDeclaration);
                if (classSymbol == null)
                    continue;

                var attributes = classSymbol.GetAttributes();
                
                AttributeData attribute = attributes.FirstOrDefault(a => a.AttributeClass?.Name == nameof(CacheAttribute));

                if (attribute == null) continue;
                
                var list = attribute.ConstructorArguments.ToList();
                List<string> typeList = new List<string>();
                foreach (var v in list)
                {
                    var str = v.ToCSharpString();
                    var strArr = str.Split(',');
                    foreach (var vv in strArr)
                    {
                        typeList.Add(vv.Replace("{", "").Replace("}", "").Replace("\"", "").Trim());
                    }
                }

                if (typeList.Count == 0) continue;


                // 获取原始文件的 using 指令
                var originalUsings = classDeclaration.SyntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .ToList();


                var source = GenerateSource(classSymbol, originalUsings, typeList);
                context.AddSource($"{classSymbol.Name}_Cache.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }

        private static string GetItem(string type, string fieldName)
        {
            if (type is "GameObject" or "Transform")
            {
                return fieldName;
            }
            return $"gameObject.GetComponent<{type}>()";
        }

        private string GenerateSource(
            INamedTypeSymbol classSymbol,
            List<UsingDirectiveSyntax> originalUsings,
            List<string> typeList)
        {
            var sb = new StringBuilder();

            // Step 1: 复制原始文件的 using 指令
            foreach (var usingDirective in originalUsings)
            {
                sb.AppendLine(usingDirective.ToFullString().Trim());
            }


            // Step 2: 处理命名空间
            bool hasNamespace = !classSymbol.ContainingNamespace.IsGlobalNamespace;
            if (hasNamespace)
            {
                sb.AppendLine();
                sb.AppendLine($"namespace {classSymbol.ContainingNamespace}");
                sb.AppendLine("{");
            }

            // Step 3: 生成类主体
            sb.AppendLine($"    public partial class {classSymbol.Name}");
            sb.AppendLine("    {");

            foreach (var v in typeList)
            {
                var typeName = v;
                if (!_type2Name.TryGetValue(typeName, out var fieldName))
                {
                    fieldName = typeName.ToLowerInvariant();
                }

                sb.AppendLine($"        private {typeName} _{fieldName};");
                sb.AppendLine();
                sb.AppendLine($"        public {typeName} {typeName}");
                sb.AppendLine("        {");
                sb.AppendLine("            get");
                sb.AppendLine("            {");
                sb.AppendLine($"                if (_{fieldName} == null)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    _{fieldName} = {GetItem(v, fieldName)};");
                sb.AppendLine("                }");
                sb.AppendLine($"                return _{fieldName};");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private sealed class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> ClassDeclarations { get; } = new List<ClassDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    ClassDeclarations.Add(classDeclarationSyntax);
                }
            }
        }
    }
}