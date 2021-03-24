// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CsToKotlinTranspiler
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Run().Wait();
        }

        private static async Task Run()
        {
            MSBuildLocator.RegisterDefaults();
            var srcPath = @"/Users/rogerjohansson/RiderProjects/ConsoleApp4/ConsoleApp4.sln";

            
            var ws = MSBuildWorkspace.Create();
            ws.WorkspaceFailed += (sender, args) =>
            {
                Console.WriteLine("Workspace failed");
            };
            
            var output =  @"/demooutput";
            var sln = await ws.OpenSolutionAsync(srcPath);
            
            Console.WriteLine(sln.Version);

            foreach (var p in sln.Projects)
            {
                Console.WriteLine($"Project {p.Name}");
                foreach (var d in p.Documents)
                {
                    var n = d.Name.ToLowerInvariant();
                    Console.WriteLine($"Document {n}");
                    if (n.Contains("assemblyinfo") || n.Contains("assemblyattributes") || !n.EndsWith(".cs"))
                    {
                        continue;
                    }

                    var model = await d.GetSemanticModelAsync();
                    var root = await d.GetSyntaxRootAsync();
                    var visitor = new KotlinTranspilerVisitor(model);
                    var res = visitor.Run(root);

                    var o = d.FilePath.Replace("democode", "demooutput");
                    var fileName = Path.ChangeExtension(o, ".kt");
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));

                    var outputFile = Path.Combine(output, fileName);
                    File.WriteAllText(outputFile, res);
                    //   return;
                }
            }
        }
    }
}