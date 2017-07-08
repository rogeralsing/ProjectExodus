// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Linq;
using System.Threading.Tasks;

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
       //     ws.WorkspaceFailed += Ws_WorkspaceFailed;
            var sln = await ws.OpenSolutionAsync(@"C:\Users\rojo01\Documents\Visual Studio 2017\Projects\Proto.Mailbox\Proto.Mailbox.sln");
            foreach (var p in sln.Projects)
            {
                //if (p.Name != "Proto.Mailbox")
                //{
                //    continue;
                //}
                foreach (var d in p.Documents)
                {
                    var n = d.Name.ToLowerInvariant();
                    if (n.Contains("assemblyinfo") || n.Contains("assemblyattributes") || !n.EndsWith(".cs"))
                    {
                        continue;
                    }
                    var model = await d.GetSemanticModelAsync();
                    var root = await d.GetSyntaxRootAsync();
                    var visitor = new KotlinTranspilerVisitor(model);
                    visitor.Visit(root);
                 //   break;
                }
            }
        }

        private static void Ws_WorkspaceFailed(object sender, Microsoft.CodeAnalysis.WorkspaceDiagnosticEventArgs e)
        {
            Console.WriteLine(e.Diagnostic.Message);
         //   throw new NotImplementedException();
        }
    }
}