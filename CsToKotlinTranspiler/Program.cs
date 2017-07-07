// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;

namespace CsToKotlinTranspiler
{
    class Program
    {
        static void Main(string[] args)
        {
            Run();
            Console.ReadLine();
        }

        private static async Task Run()
        {
            var ws = MSBuildWorkspace.Create();
            var sln = await ws.OpenSolutionAsync(@"C:\git\ProjectExodus\CsToKotlinTranspiler.sln");
            foreach (var p in sln.Projects)
            {
                foreach (var d in p.Documents)
                {
                    var model = await d.GetSemanticModelAsync();
                    var root = await d.GetSyntaxRootAsync();
                    var visitor = new CsToKotlinTranspiler.KotlinTranspilerVisitor(model);
                    visitor.Visit(root);
                    break;
                }
            }
        }
    }
}