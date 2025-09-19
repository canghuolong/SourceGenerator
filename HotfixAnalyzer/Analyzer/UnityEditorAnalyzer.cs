using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sun.SourceGenerator.Generator
{
    [Generator]
    public class UnityEditorChecker : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor Rule = new(
            "UE001",
            "UnityEditor引用检查",
            "检测到代码中使用了UnityEditor: '{0}'",
            "Unity",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "在源代码生成器项目中不应引用UnityEditor命名空间。"
        );

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new UsingDirectiveSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var assemblyName = context.Compilation.AssemblyName;

            if (assemblyName != "Game.Hotfix")
            {
                return;
            }
            
            if (context.SyntaxReceiver is not UsingDirectiveSyntaxReceiver receiver)
                return;

            foreach (var usingDirective in receiver.UsingDirectives)
            {
                var name = usingDirective.Name.ToString();
                if (name.StartsWith("UnityEditor"))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        usingDirective.GetLocation(),
                        name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private class UsingDirectiveSyntaxReceiver : ISyntaxReceiver
        {
            public List<UsingDirectiveSyntax> UsingDirectives { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is UsingDirectiveSyntax usingDirective)
                {
                    UsingDirectives.Add(usingDirective);
                }
            }
        }
    }
}
