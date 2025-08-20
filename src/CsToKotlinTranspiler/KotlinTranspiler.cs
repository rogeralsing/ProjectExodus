using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsToKotlinTranspiler;

/// <summary>
/// Helper facade used from tests to transpile small snippets of C# code
/// to Kotlin without needing an MSBuild workspace.
/// </summary>
public static class KotlinTranspiler
{
    public static string Transpile(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "TranspilerTests",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var visitor = new KotlinTranspilerVisitor(model);
        return visitor.Run(tree.GetRoot());
    }
}
