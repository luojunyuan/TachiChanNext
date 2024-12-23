using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TouchChan.SourceGenerators
{
    [Generator]
    public class LRUCacheGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            foreach (var method in receiver.CandidateMethods)
            {
                var semanticModel = context.Compilation.GetSemanticModel(method.SyntaxTree);
                var methodSymbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;
                var classSymbol = methodSymbol?.ContainingType;

                if (methodSymbol == null || classSymbol == null)
                    continue;

                var className = classSymbol.Name;
                var methodName = methodSymbol.Name;
                var returnType = methodSymbol.ReturnType.ToDisplayString();
                var parameterList = string.Join(", ", methodSymbol.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
                var argumentList = string.Join(", ", methodSymbol.Parameters.Select(p => p.Name));

                var source = $@"
using Microsoft.Extensions.Caching.Memory;
using System;

namespace {classSymbol.ContainingNamespace}
{{
    public partial class {className}
    {{
        private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

        public static {returnType} {methodName}_Cached({parameterList})
        {{
            var cacheKey = $""{methodName}({argumentList})"";
            if (!_cache.TryGetValue(cacheKey, out {returnType} result))
            {{
                result = {methodName}({argumentList});
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .SetPriority(CacheItemPriority.High);

                _cache.Set(cacheKey, result, cacheEntryOptions);
            }}

            return result;
        }}
    }}
}}
";
                context.AddSource($"{className}_{methodName}_Cached.g.cs", source);
            }
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is MethodDeclarationSyntax methodDeclarationSyntax &&
                    methodDeclarationSyntax.AttributeLists.Count > 0)
                {
                    CandidateMethods.Add(methodDeclarationSyntax);
                }
            }
        }
    }
}
