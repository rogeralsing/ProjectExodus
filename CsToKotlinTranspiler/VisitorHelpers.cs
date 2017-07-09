// -----------------------------------------------------------------------
//   <copyright file="VisitorHelpers.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsToKotlinTranspiler
{
    public partial class KotlinTranspilerVisitor
    {
        private readonly StringBuilder _sb = new StringBuilder();
        

        private string GetKotlinType(TypeSyntax type)
        {
            if (type is ArrayTypeSyntax arr)
            {
                var t = GetKotlinType(arr.ElementType);
                return $"arrayOf<{t}>";
            }

            var si = _model.GetSymbolInfo(type);
            var s = si.Symbol;
            if (s == null)
            {
                return "**unknown**"; //TODO: how does this work?
            }
            return GetKotlinType(s as ITypeSymbol);
        }

        private string GetKotlinType(ITypeSymbol s)
        {
            var named = s as INamedTypeSymbol;
            if (named != null)
            {
                if (s.TypeKind == TypeKind.Delegate)
                {
                    var args = named.DelegateInvokeMethod.Parameters.Select(p => p.Type).Select(GetKotlinType);
                    var ret = GetKotlinType(named.DelegateInvokeMethod.ReturnType);
                    return $"({string.Join(", ", args)}) -> {ret}";
                }

                if (named?.IsGenericType == true)
                {
                    var name = named.Name;
                    switch (name)
                    {
                        case "ConcurrentQueue":
                            name = "ConcurrentLinkedQueue";
                            break;
                    }

                    var args = named.TypeArguments.Select(GetKotlinType);
                    return $"{name}<{string.Join(", ", args)}>";
                }
            }

            if (s.Kind == SymbolKind.ArrayType)
            {
                var arr = (IArrayTypeSymbol) s;
                return $"Array<{GetKotlinType(arr.ElementType)}>";
            }
            var str = s.Name;
            switch (str)
            {
                case "Void":
                    return "Unit";
                case "TimeSpan":
                    return "Duration";
                case "Object":
                    return "Any";
                case "Int32":
                    return "Int";
                case "Boolean":
                    return "Boolean";
                case "String":
                    return "String";
                case "ArgumentException":
                    return "IllegalArgumentException";
            }

            return str;
        }

        public string GetKotlinPackageName(string ns)
        {
            return ns.ToLowerInvariant();
        }

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

                    return p.Identifier + " : " + GetKotlinType(p.Type);
                }).ToList();
            }

            var arg = string.Join(", ", GetArgumentList(node));
            return arg;
        }

        private void Indent(string text)
        {
            Write(GetIndent() + text);
        }

        private void Indent()
        {
            Write(GetIndent());
        }

        private void Write(string text)
        {
            Console.Write(text);
            _sb.Append(text);
        }

        private void NewLine()
        {
            Write("\n");
        }

        private void WriteLine(string text)
        {
            Write(GetIndent() + text);
            NewLine();
        }

        private string GetIndent()
        {
            return new string(' ', _indent * 4);
        }

        private static string ToCamelCase(string name)
        {
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static bool FieldIsReadOnly(FieldDeclarationSyntax node)
        {
            return node.Modifiers.Any(m => m.Text == "readonly" || m.Text=="const");
        }

        private bool IsInterfaceMethod(MethodDeclarationSyntax node)
        {
            var methodSymbol = _model.GetDeclaredSymbol(node);
            bool isInterfaceMethod = methodSymbol.ContainingType
                                                 .AllInterfaces
                                                 .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
                                                 .Any(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }

        private bool IsInterfaceProperty(PropertyDeclarationSyntax node)
        {
            var methodSymbol = _model.GetDeclaredSymbol(node);
            bool isInterfaceMethod = methodSymbol.ContainingType
                                                 .AllInterfaces
                                                 .SelectMany(@interface => @interface.GetMembers().OfType<IPropertySymbol>())
                                                 .Any(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }

        private string GetKotlinDefaultValue(TypeSyntax type)
        {
            var si = _model.GetSymbolInfo(type);
            var s = si.Symbol;
            var str = s.Name;
            switch (str)
            {
                case "Int32":
                    return "0";
                case "Boolean":
                    return "false";
                case "String":
                    return "\"\"";
            }
            return null;
        }

        public string Run(SyntaxNode root)
        {
            Visit(root);
            return _sb.ToString();
        }
    }
}