using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
