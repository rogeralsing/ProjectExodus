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
                    methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }

        private bool IsInterfaceProperty(PropertyDeclarationSyntax node)
        {
            var methodSymbol = _model.GetDeclaredSymbol(node);
            var isInterfaceMethod = methodSymbol.ContainingType
                .AllInterfaces
                .SelectMany(@interface => @interface.GetMembers().OfType<IPropertySymbol>())
                .Any(method =>
                    methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }


        public string Run(SyntaxNode root)
        {
            Setup();
            _assignments = (from exp in root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                let symbol = _model.GetSymbolInfo(exp.Left).Symbol
                where symbol != null
                group exp by symbol).ToDictionary(g => g.Key, g => g.ToArray());

            Visit(root);
            return _sb.ToString();
        }
    }
}