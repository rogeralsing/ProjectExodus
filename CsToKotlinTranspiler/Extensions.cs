using System.Linq;
using Microsoft.CodeAnalysis;

namespace CsToKotlinTranspiler
{
    public static class Extensions
    {
        public static bool Contains(this SyntaxTokenList mods, string mod)
        {
            return mods.Any(t => t.Text == mod);
        }
    }
}