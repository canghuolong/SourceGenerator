namespace Sun.SourceGenerator.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[Generator]
public class RCGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }
    
    private static bool HasRCAttributeMembers(INamedTypeSymbol classSymbol, INamedTypeSymbol attributeSymbol)
    {
        return classSymbol.GetMembers()
            .Any(m => m.GetAttributes()
                .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol)));
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            return;
        
        
        var attributeSymbol = context.Compilation.GetTypeByMetadataName("Sun.SourceGenerator.Attributes.RCAttribute");
        if(attributeSymbol == null)return;

        foreach (var classDecl in receiver.CandidateClasses)
        {
            var model = context.Compilation.GetSemanticModel(classDecl.SyntaxTree);
            var classSymbol = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
            if(classSymbol == null)continue;

            var hasRcMembers = HasRCAttributeMembers(classSymbol, attributeSymbol);
            if(!hasRcMembers)continue;
            // 验证 partial 修饰符
            if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "RC1001",
                        "Partial class required",
                        "Class '{0}' must be partial",
                        "Design",
                        DiagnosticSeverity.Error,
                        true),
                    classDecl.GetLocation(),
                    classSymbol.Name));
                continue;
            }

            var members = CollectMembers(classSymbol, attributeSymbol);
            if (members.Count == 0) continue;
            
            // 获取原始文件的 using 指令
            var originalUsings = classDecl.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .ToList();
            

            var source = GenerateClassCode(classSymbol, members,originalUsings,classDecl.Modifiers);
            context.AddSource($"{classSymbol.Name}_RC.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private List<MemberInfo> CollectMembers(INamedTypeSymbol classSymbol, INamedTypeSymbol attributeSymbol)
    {
        var members = new List<MemberInfo>();

        foreach (var member in classSymbol.GetMembers())
        {
            var attributes = member.GetAttributes()
                .Where(a => a.AttributeClass?.Equals(attributeSymbol, SymbolEqualityComparer.Default) == true);

            foreach (var attr in attributes)
            {
                if (member is IFieldSymbol field)
                {
                    members.Add(new MemberInfo(
                        field.Name,
                        attr.ConstructorArguments[0].Value?.ToString(),
                        field.Type,
                        true
                    ));
                }
                else if (member is IPropertySymbol prop && prop.SetMethod != null)
                {
                    members.Add(new MemberInfo(
                        prop.Name,
                        attr.ConstructorArguments[0].Value?.ToString(),
                        prop.Type,
                        false
                    ));
                }
            }
        }

        return members;
    }

    private string GenerateClassCode(INamedTypeSymbol classSymbol, List<MemberInfo> members,List<UsingDirectiveSyntax> originalUsings,
        SyntaxTokenList classModifiers)
    {
        var ns = classSymbol.ContainingNamespace.ToDisplayString();
        var className = classSymbol.Name;

        var sb = new StringBuilder();
        
        foreach (var usingDirective in originalUsings)
        {
            sb.AppendLine(usingDirective.ToFullString().Trim());
        }
        

        if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.AppendLine($"namespace {ns}\n{{");
        }

        var modifiers = string.Join(" ", classModifiers.Select(m => m.Text));
        
        sb.AppendLine($@"
    {modifiers} class {className}
    {{
        public void LoadFromRC(ReferenceCollector rc)
        {{");

        foreach (var member in members)
        {
            var assignment = GenerateAssignment(member);
            sb.AppendLine($"            {assignment}");
        }

        sb.AppendLine(@"        }
    }");
        if (!classSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private string GenerateAssignment(MemberInfo member)
    {
        var typeName = GetTypeDisplayString(member.Type);
        if (string.IsNullOrEmpty(member.ConfigKey))
        {
            return $"{member.Name} = rc.Get<{typeName}>(\"{member.Name}\");";
        }

        return $"{member.Name} = rc.Get<{typeName}>(\"{member.ConfigKey}\");";
    }

    private string GetTypeDisplayString(ITypeSymbol typeSymbol)
    {
        // 处理可空类型
        if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return $"{GetTypeDisplayString(namedType.TypeArguments[0])}?";
        }

        // 处理特殊类型显示格式
        return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace("System.", "");
    }
}

internal class SyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not ClassDeclarationSyntax classDecl)
        {
            return;
        }

        var members = classDecl.Members;
        foreach (var v in members)
        {
            if (v is FieldDeclarationSyntax or PropertyDeclarationSyntax)
            {
                if (v.AttributeLists.Count > 0)
                {
                    if (!CandidateClasses.Contains(classDecl))
                    {
                        CandidateClasses.Add(classDecl);    
                    }
                    break;
                }
            }
        }
    }
}

internal record MemberInfo(string Name, string ConfigKey, ITypeSymbol Type, bool IsField)
{
    public string Name { get; } = Name;
    public string ConfigKey { get; } = ConfigKey;
    public ITypeSymbol Type { get; } = Type;

    public bool IsField { get; } = IsField;
}