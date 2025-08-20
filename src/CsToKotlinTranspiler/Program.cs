// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CsToKotlinTranspiler
{
    internal class Program
    {
        // Entry point is asynchronous to avoid blocking and to enable awaiting
        // asynchronous operations throughout the startup sequence.
        private static async Task Main(string[] args)
        {
            await Run(args);
        }

        /// <summary>
        ///     Runs the transpiler. The first argument (or environment variable
        ///     <c>CS2KOTLIN_SRC</c>) specifies the solution to transpile. The
        ///     second argument (or <c>CS2KOTLIN_OUT</c>) specifies the output
        ///     directory for generated Kotlin files. Defaults are used when
        ///     neither command line arguments nor environment variables are
        ///     supplied.
        /// </summary>
        /// <param name="args">Optional command line arguments.</param>
        private static async Task Run(string[] args)
        {
            MSBuildLocator.RegisterDefaults();

            const string defaultSrc = "CsToKotlinTranspiler.sln";
            const string defaultOut = "kotlinOutput";

            var srcPath = args.Length > 0
                ? args[0]
                : Environment.GetEnvironmentVariable("CS2KOTLIN_SRC") ?? defaultSrc;

            var output = args.Length > 1
                ? args[1]
                : Environment.GetEnvironmentVariable("CS2KOTLIN_OUT") ?? defaultOut;

            var ws = MSBuildWorkspace.Create();
            ws.WorkspaceFailed += (sender, args) =>
            {
                Console.WriteLine($"Workspace failed: {args.Diagnostic}");
            };

            var sln = await ws.OpenSolutionAsync(srcPath);
            var slnDir = Path.GetDirectoryName(srcPath);
            if (string.IsNullOrEmpty(slnDir))
            {
                slnDir = Directory.GetCurrentDirectory();
            }
            
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

                    var relative = Path.GetRelativePath(slnDir, d.FilePath);
                    var outputFile = Path.Combine(output, Path.ChangeExtension(relative, ".kt"));
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                    File.WriteAllText(outputFile, res);
                    //   return;
                }
            }
        }
    }
}
