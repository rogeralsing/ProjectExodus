// -----------------------------------------------------------------------
//   <copyright file="TypeHelpers.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsToKotlinTranspiler
{
    public partial class KotlinTranspilerVisitor
    {
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
                var arr = (IArrayTypeSymbol) s;
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
                    var name = GetGenericName(named.Name);

                    var args = named.TypeArguments.Select(GetKotlinType);
                    return $"{name}<{string.Join(", ", args)}>";
                }
            }

            return GetName(s.Name);
        }

        public static string GetName(string name)
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
                default: return name;
            }
        }

        public static string GetGenericName(string name)
        {
            switch (name)
            {
                case nameof(List<object>): return "MutableList";
                case nameof(ISet<object>):
                case nameof(HashSet<object>): return "MutableSet";
                case nameof(Stack<object>): return "Stack";
                case nameof(ConcurrentQueue<object>): return "ConcurrentLinkedQueue";
                case nameof(ConcurrentDictionary<object, object>): return "ConcurrentHashMap";
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
            var str = s.Name;
            switch (str)
            {
                case nameof(TimeSpan): return "Duration.ZERO";
                case nameof(Int64): return "0";
                case nameof(Int32): return "0";
                case nameof(Double): return "0.0";
                case nameof(Single): return "0.0";
                case nameof(Boolean): return "false";
                case nameof(String): return null;
                case nameof(List<object>): return null;
                case nameof(ISet<object>):
                case nameof(HashSet<object>): return null;
                case nameof(Stack<object>): return null;
                case nameof(ConcurrentQueue<object>): return null;
                case nameof(ConcurrentDictionary<object, object>): return null;
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
                    var t = GetKotlinType(s);
                    return $"{t}()"; //structs are initialized to empty ctor
                }
            }

            return null;
        }
    }
}