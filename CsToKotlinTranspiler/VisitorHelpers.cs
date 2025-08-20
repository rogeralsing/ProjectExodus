// -----------------------------------------------------------------------
//   <copyright file="VisitorHelpers.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsToKotlinTranspiler
{
    public partial class KotlinTranspilerVisitor
    {
        public string GetKotlinPackageName(string ns) => ns.ToLowerInvariant();

        private string GetArgList(ParameterListSyntax node)
        {
            List<string> GetArgumentList(ParameterListSyntax parameterList)
            {
                return parameterList.Parameters.Select(p =>
                {
                    if (p.Type == null)
                    {
                        return p.Identifier.ToString();
                    }

                    return p.Identifier + " : " + TranslateType(p.Type);
                }).ToList();
            }

            var arg = string.Join(", ", GetArgumentList(node));
            return arg;
        }

        private static string ToCamelCase(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

        private static bool FieldIsReadOnly(FieldDeclarationSyntax node)
        {
            return node.Modifiers.Any(m => m.Text == "readonly" || m.Text == "const");
        }

        private bool IsInterfaceMethod(MethodDeclarationSyntax node)
        {
            var methodSymbol = _model.GetDeclaredSymbol(node);
            var isInterfaceMethod = methodSymbol.ContainingType
                .AllInterfaces
                .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
                .Any(method =>
                    SymbolEqualityComparer.Default.Equals(
                        methodSymbol,
                        methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }

        private bool IsInterfaceProperty(PropertyDeclarationSyntax node)
        {
            var methodSymbol = _model.GetDeclaredSymbol(node);
            var isInterfaceMethod = methodSymbol.ContainingType
                .AllInterfaces
                .SelectMany(@interface => @interface.GetMembers().OfType<IPropertySymbol>())
                .Any(method =>
                    SymbolEqualityComparer.Default.Equals(
                        methodSymbol,
                        methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }


        public string Run(SyntaxNode root)
        {
            Setup();
            _assignments = root
                .DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                // Track each assignment alongside the symbol it updates
                .Select(exp => new { exp, symbol = _model.GetSymbolInfo(exp.Left).Symbol })
                .Where(x => x.symbol != null)
                // Group by the symbol using Roslyn's equality comparer to avoid analyzer warnings
                .GroupBy(x => x.symbol!, SymbolEqualityComparer.Default)
                // Build a lookup from symbol to its assignments
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.exp).ToArray(),
                    SymbolEqualityComparer.Default);

            Visit(root);
            return _sb.ToString();
        }
    }
}