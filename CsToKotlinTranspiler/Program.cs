// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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
            var myPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            var dir = System.IO.Path.GetDirectoryName(myPath);
            var srcPath = Path.Combine(dir, @"..\..\..");

            var ws = MSBuildWorkspace.Create();
            var output = srcPath + @"\demooutput";
            var sln = await ws.OpenSolutionAsync(srcPath + @"\democode\DemoCode.sln");

            var compilations = await Task.WhenAll(sln.Projects.Select(x => x.GetCompilationAsync()));

            foreach (var p in sln.Projects)
            {
                var c = await p.GetCompilationAsync();
                foreach (var d in p.Documents)
                {
                    var n = d.Name.ToLowerInvariant();
                    ////if (n != "program.cs")
                    ////{
                    ////    continue;
                    ////}
                    if (n.Contains("assemblyinfo") || n.Contains("assemblyattributes") || !n.EndsWith(".cs"))
                    {
                        continue;
                    }

                    var model = await d.GetSemanticModelAsync();

                    var root = await d.GetSyntaxRootAsync();

                    var visitor = new KotlinTranspilerVisitor(model);
                    var res = visitor.Run(root);
                    var fileName = Path.ChangeExtension(d.Name, ".kt");
                    var outputFile = Path.Combine(output, fileName);
                    File.WriteAllText(outputFile, res);
                }
            }
        }

        private static void Ws_WorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
        {
            Console.WriteLine(e.Diagnostic.Message);
            //   throw new NotImplementedException();
        }
    }
}