// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;

namespace CsToKotlinTranspiler
{
    class Program
    {
        static void Main(string[] args)
        {
            Run().Wait();
            Console.ReadLine();
        }

        private static async Task Run()
        {
            var myPath = Assembly.GetExecutingAssembly().Location;

            var dir = Path.GetDirectoryName(myPath);
            var srcPath = Path.GetFullPath(Path.Combine(dir, @"..\..\.."));

            var ws = MSBuildWorkspace.Create();
            var output = srcPath + @"\demooutput";
            var sln = await ws.OpenSolutionAsync(srcPath + @"\democode\DemoCode.sln");

            foreach (var p in sln.Projects)
            {
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