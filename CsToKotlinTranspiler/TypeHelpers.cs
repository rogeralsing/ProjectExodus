// -----------------------------------------------------------------------
//   <copyright file="TypeHelpers.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpExtensions = Microsoft.CodeAnalysis.CSharp.CSharpExtensions;

namespace CsToKotlinTranspiler
{
    public partial class KotlinTranspilerVisitor
    {
        private string TranslateType(TypeSyntax type)
        {
            return TranslateType(GetTypeSymbol(type));
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

        private string TranslateType(ITypeSymbol s)
        {
            switch (s.Kind)
            {
                case SymbolKind.ArrayType:
                    var arr = (IArrayTypeSymbol) s;
                    return $"Array<{TranslateType(arr.ElementType)}>";
                case SymbolKind.NamedType:
                    var named = (INamedTypeSymbol) s;
                    switch (s.TypeKind)
                    {
                        case TypeKind.TypeParameter: return named.Name;
                        case TypeKind.Struct: return GetKnownName(s.Name);
                        case TypeKind.Delegate: return TranslateDelegateType(named);
                        case TypeKind.Class:
                        {
                            if (named?.Name == "Task")
                            {
                                if (!named.IsGenericType)
                                {
                                    return "Unit";
                                }
                                var arg = named.TypeArguments.First();
                                return TranslateType(arg);
                            }

                            if (named.IsGenericType)
                            {
                                return TranslateGenericType(named);
                            }

                            return GetKnownName(s.Name);
                            }
                        case TypeKind.Interface:
                        {
                            var res = GetKnownName(s.Name);
                            if (res.StartsWith("I") && char.IsUpper(res[1]))
                            {
                                res = res.Substring(1); //remove I-prefix of interface
                            }
                            return res;
                        }
                    }
                    break;
            }

            return GetKnownName(s.Name);
        }

        private string TranslateGenericType(INamedTypeSymbol named)
        {
            var name = GetKnownGenericName(named.Name);
            var args = named.TypeArguments.Select(TranslateType).ToArray();
            var joined = string.Join(", ", args);
            return $"{name}<{joined}>";
        }

        private string TranslateDelegateType(INamedTypeSymbol named)
        {
            var args = named.DelegateInvokeMethod.Parameters.Select(p => p.Type).Select(TranslateType).ToArray();
            var joined = string.Join(", ", args);
            var ret = TranslateType(named.DelegateInvokeMethod.ReturnType);
            var isAsync = IsAsync(named.DelegateInvokeMethod.ReturnType);
            return isAsync ? "suspend " : "" + $"({joined}) -> {ret}";
        }

        public static string GetKnownName(string name)
        {
            switch (name)
            {
                case "Void": return "Unit";
                
                case nameof(Object): return "Any";
                case nameof(Int32): return "Int";
                case nameof(Int64): return "Long";
                case nameof(Double): return "Double";
                case nameof(Single): return "Single";
                case nameof(Boolean): return "Boolean";
                case nameof(String): return "String";
                case nameof(TimeSpan): return "Duration";
                case nameof(ArgumentException): return "IllegalArgumentException";
                case nameof(ManualResetEventSlim):
                case nameof(ManualResetEvent):
                case nameof(AutoResetEvent): return "CountDownLatch";
                default: return name;
            }
        }

        public static string GetKnownGenericName(string name)
        {
            switch (name)
            {
                case nameof(List<object>): return "MutableList";
                case nameof(ISet<object>):
                case nameof(HashSet<object>): return "MutableSet";
                case nameof(Stack<object>): return "Stack";
                case nameof(ConcurrentQueue<object>): return "ConcurrentLinkedQueue";
                case nameof(ConcurrentDictionary<object, object>): return "ConcurrentHashMap";
                case nameof(IReadOnlyCollection<object>): return "Collection";
                default: return name;
            }
        }

        private string GetKotlinDefaultValue(TypeSyntax type)
        {
            var s = GetTypeSymbol(type);
            return GetKotlinDefaultValue(s);
        }

        private string GetKotlinDefaultValue(ITypeSymbol s)
        {
            switch (s.Name)
            {
                case nameof(TimeSpan): return "Duration.ZERO";
                case nameof(Int64): return "0";
                case nameof(Int32): return "0";
                case nameof(Double): return "0.0";
                case nameof(Single): return "0.0";
                case nameof(Boolean): return "false";
                default:
                    switch (s.TypeKind)
                    {
                        case TypeKind.Array: return "arrayOf()";
                        case TypeKind.Enum:
                            var named = (INamedTypeSymbol) s;
                            return $"{named.Name}.{named.MemberNames.First()}";
                        case TypeKind.Struct:
                            var t = TranslateType(s);
                            return $"{t}()"; //structs are initialized to empty ctor
                        default: return null;
                    }
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            switch (node.Expression)
            {
                case MemberAccessExpressionSyntax member:
                    var methodName = member.Name.Identifier.Text;
                    var sym = CSharpExtensions.GetSymbolInfo(_model, node).Symbol;
                    var containingTypeName = sym?.ContainingType?.Name;

                    switch (containingTypeName)
                    {
                        //case nameof(AutoResetEvent):
                        //case nameof(ManualResetEvent):
                        //case nameof(ManualResetEventSlim):
                        case nameof(EventWaitHandle):
                            switch (methodName)
                            {
                                case nameof(AutoResetEvent.Set):
                                    Visit(member.Expression);
                                    Write(".countDown()");
                                    break;
                            }
                            break;
                        case nameof(Enumerable):
                            switch (methodName)
                            {
                                case nameof(Enumerable.Select):
                                    Visit(node.Expression);
                                    Write(".");
                                    Write("map");
                                    Visit(node.ArgumentList);
                                    break;
                                case nameof(Enumerable.Where):
                                    Visit(node.Expression);
                                    Write(".");
                                    Write("filter");
                                    Visit(node.ArgumentList);
                                    break;
                                case nameof(Enumerable.ToList):
                                    Visit(node.Expression);
                                    Write(".");
                                    Write("toList");
                                    Visit(node.ArgumentList);
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case nameof(Task):
                            switch (methodName)
                            {
                                case nameof(Task.FromResult):
                                    Write("");
                                    break;
                            }
                            break;
                        case nameof(Console):
                            switch (methodName)
                            {
                                case nameof(Console.WriteLine):
                                    Write("println");
                                    Visit(node.ArgumentList);
                                    break;
                                case nameof(Console.Write):
                                    Write("print");
                                    Visit(node.ArgumentList);
                                    break;
                                case nameof(Console.ReadLine):
                                    Write("readLine");
                                    Visit(node.ArgumentList);
                                    break;
                            }
                            break;
                        default:
                            Visit(member.Expression);
                            Write(".");
                            var name = member.Name.ToString();
                            if (sym.Kind == SymbolKind.Method || sym.Kind == SymbolKind.Property)
                            {
                                name = ToCamelCase(name);
                            }

                            Write(name);
                            Visit(node.ArgumentList);
                            break;
                    }
                    break;
                //  id.Call()
                case IdentifierNameSyntax _:                
                // .Call()
                case MemberBindingExpressionSyntax _:      
                    Visit(node.Expression);
                    Visit(node.ArgumentList);
                    break;
                default:
                    throw new NotSupportedException();
            }
           
        }
    }
}