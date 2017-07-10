using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsToKotlinTranspiler
{
    public static class TypeHelpers
    {
        public static string GetName(string name)
        {
            switch (name)
            {
                case "Void": return "Unit";
                case nameof(TimeSpan): return "Duration";
                case nameof(Object): return "Any";
                case nameof(Int32): return "Int";
                case nameof(Boolean): return "Boolean";
                case nameof(String): return "String";
                case nameof(ArgumentException): return "IllegalArgumentException";
                default: return name;
            }
        }

        public static string GetGenericName(string name)
        {
            switch (name)
            {
                case nameof(ConcurrentQueue<object>): return "ConcurrentLinkedQueue";
                case nameof(ConcurrentDictionary<object, object>): return "ConcurrentHashMap";
                case nameof(List<object>): return "MutableList";
                case nameof(ISet<Object>):
                case nameof(HashSet<Object>): return "MutableSet";
                case nameof(Stack<object>): return "Stack";
                default: return name;
            }
        }
    }
}
