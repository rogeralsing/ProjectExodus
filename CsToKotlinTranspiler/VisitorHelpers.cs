// -----------------------------------------------------------------------
//   <copyright file="VisitorHelpers.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            return GetKotlinType(GetTypeSymbol(type));
        }

        private ITypeSymbol GetTypeSymbol(TypeSyntax type)
        {
            var ti = _model.GetTypeInfo(type);
            if (ti.Type != null)
            {
                return ti.Type;
            }
            var s = _model.GetSymbolInfo(type).Symbol;
            if (s != null)
            {
                return s as ITypeSymbol;
            }

            throw new NotSupportedException("Unknown TypeSyntax");
        }

        private string GetKotlinType(ITypeSymbol s)
        {
            if (s.Kind == SymbolKind.ArrayType)
            {
                var arr = (IArrayTypeSymbol)s;
                return $"Array<{GetKotlinType(arr.ElementType)}>";
            }

            if (s is INamedTypeSymbol named)
            {
                if (s.TypeKind == TypeKind.Delegate)
                {
                    var args = named.DelegateInvokeMethod.Parameters.Select(p => p.Type).Select(GetKotlinType);
                    var ret = GetKotlinType(named.DelegateInvokeMethod.ReturnType);
                    return $"({string.Join(", ", args)}) -> {ret}";
                }

                if (named.IsGenericType)
                {
                    var name = TypeHelpers.GetGenericName(named.Name);

                    var args = named.TypeArguments.Select(GetKotlinType);
                    return $"{name}<{string.Join(", ", args)}>";
                }
            }


            return TypeHelpers.GetName(s.Name);
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

        private void IndentWrite(string text)
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

        private void IndentWriteLine(string text)
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
            return node.Modifiers.Any(m => m.Text == "readonly" || m.Text == "const");
        }

        private bool IsInterfaceMethod(MethodDeclarationSyntax node)
        {
            var methodSymbol = _model.GetDeclaredSymbol(node);
            var isInterfaceMethod = methodSymbol.ContainingType
                                                .AllInterfaces
                                                .SelectMany(@interface => @interface.GetMembers().OfType<IMethodSymbol>())
                                                .Any(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }

        private bool IsInterfaceProperty(PropertyDeclarationSyntax node)
        {
            var methodSymbol = _model.GetDeclaredSymbol(node);
            var isInterfaceMethod = methodSymbol.ContainingType
                                                .AllInterfaces
                                                .SelectMany(@interface => @interface.GetMembers().OfType<IPropertySymbol>())
                                                .Any(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method)));
            return isInterfaceMethod;
        }

        private string GetKotlinDefaultValue(TypeSyntax type)
        {
            var s = GetTypeSymbol(type);
            var str = s.Name;
            switch (str)
            {
                case nameof(Int64): return "0";
                case nameof(Int32): return "0";
                case nameof(Double): return "0.0";
                case nameof(Single): return "0.0";
                case nameof(Boolean): return "false";
                case nameof(String): return null;
                case nameof(List<object>): return null;
                case nameof(ISet<object>):
                case nameof(HashSet<object>): return null;
            }
            if (s.TypeKind == TypeKind.Interface)
            {
                return null;
            }
            if (s.TypeKind == TypeKind.Array)
            {
                return "arrayOf()";
            }
            if (s is INamedTypeSymbol named)
            {
                if (named.TypeKind == TypeKind.Enum)
                {
                    return $"{named.Name}.{named.MemberNames.First()}";
                }

                if (named.TypeKind == TypeKind.Struct)
                {
                    var t = GetKotlinType(type);
                    return $"{t}()"; //structs are initialized to empty ctor
                }
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