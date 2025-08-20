using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Spectre.Console;
using System.Text.RegularExpressions;

using CsToKotlinTranspiler;

namespace CsToKotlinCli;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            ShowHelp();
            return;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]File not found:[/] {path}");
            return;
        }

        var code = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(code);

        // Set up a minimal compilation so the semantic model can be retrieved.
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "Transpilation",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var visitor = new KotlinTranspilerVisitor(model);
        var result = visitor.Run(tree.GetRoot());

        AnsiConsole.Write(new Rule("[yellow]Kotlin Output[/]"));
        AnsiConsole.MarkupLine(HighlightKotlin(result));
    }

    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Transpiles a single C# file to Kotlin and prints the result.[/]");
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[underline]Usage[/]:");
        AnsiConsole.MarkupLine("  dotnet run --project CsToKotlinCli -- <path-to-csharp-file>");
    }

    private static string HighlightKotlin(string code)
    {
        var escaped = Markup.Escape(code);
        var keywords = new[] { "package", "class", "fun", "var", "val", "if", "else", "return", "for", "while", "when", "in", "is" };
        foreach (var kw in keywords)
        {
            escaped = Regex.Replace(escaped, $"\\b{kw}\\b", $"[cyan]{kw}[/]");
        }

        var types = new[] { "Int", "String", "Unit", "List", "Array" };
        foreach (var type in types)
        {
            escaped = Regex.Replace(escaped, $"\\b{type}\\b", $"[green]{type}[/]");
        }

        return escaped;
    }
}

