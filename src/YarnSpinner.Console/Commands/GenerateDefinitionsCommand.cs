namespace YarnSpinnerConsole
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Build.Locator;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using System;

    public static class GenerateDefinitionsCommand
    {
        public static void GenerateDefinitions(DirectoryInfo inputDirectory, DirectoryInfo outputDirectory, string projectType)
        {
            switch (projectType)
            {
                case "Unity":
                    GenerateYSLSFilesForUnity(inputDirectory, outputDirectory);
                    break;
                case "Godot-gd":
                    Log.Error("At this stage ysc only supports generating YSLS files for Unity");
                    System.Environment.Exit(1);
                    break;
                case "Godot-csharp":
                    Log.Warn("At this stage ysc only supports generating YSLS files for Unity but will attempt to read the Godot C# as Unity C#");
                    GenerateYSLSFilesForUnity(inputDirectory, outputDirectory);
                    Log.PrintLine("Processing of the files is complete, recommend double checking the YSLS for incompatibility with Godot.");
                    break;
                case "Unreal":
                    Log.Error("At this stage ysc only supports generating YSLS files for Unity");
                    System.Environment.Exit(1);
                    break;
                default:
                    Log.Error($"\"{projectType}\" is an unsupported engine.");
                    System.Environment.Exit(1);
                    break;
            }
        }

        private static void GenerateYSLSFilesForUnity(DirectoryInfo inputDirectory, DirectoryInfo outputDirectory)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().FirstOrDefault();
            if (instances == null)
            {
                Log.Error("Unable to find a working MSBuild, so cannot continue.");
                System.Environment.Exit(1);
            }

            MSBuildLocator.RegisterInstance(instances);

            if (inputDirectory == null)
            {
                Log.Error("The input directory is null");
                System.Environment.Exit(1);
            }

            var prefixesToIgnore = new List<string>()
            {
                "YarnSpinner.Unity",
                "YarnSpinner.Editor",
            };
            // But DO generate source code for the Samples assembly.
            var prefixesToKeep = new List<string>()
            {
                "YarnSpinner.Unity.Samples",
            };

            var projects = inputDirectory.GetFiles("*.csproj");
            if (projects.Length > 0)
            {
                Log.Info($"Found {projects.Length} csproj file(s) in {inputDirectory.FullName}");
            }
            else
            {
                Log.Error($"No csproj files found in {inputDirectory.FullName}");
                System.Environment.Exit(1);
            }
            var logger = new NullLogger();
            foreach (var projectPath in projects)
            {
                MSBuildWorkspace workspace = MSBuildWorkspace.Create();
                var project = workspace.OpenProjectAsync(projectPath.FullName).Result;

                var compilation = project.WithParseOptions(CSharpParseOptions.Default).GetCompilationAsync().Result as CSharpCompilation;
                var assemblyName = compilation.AssemblyName ?? "NULL";

                if (compilation.ReferencedAssemblyNames.Count(name => name.Name == "YarnSpinner.Unity") == 0)
                {
                    continue;
                }

                if (prefixesToIgnore.Any(prefix => assemblyName.StartsWith(prefix)) && !prefixesToKeep.Any(prefix => assemblyName.StartsWith(prefix)))
                {
                    continue;
                }

                List<Action> actions = new List<Action>();
                foreach (var tree in compilation.SyntaxTrees)
                {
                    actions.AddRange(Analyser.GetActions(compilation, tree, logger));
                }

                if (actions.Count > 0)
                {
                    foreach (var action in actions)
                    {
                        if (action.Validate(compilation, logger).Any(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error))
                        {
                            action.ContainsErrors = true;
                        }
                    }
                    IEnumerable<string> commandJSON = actions.Where(a => a.Type == ActionType.Command).Select(a => a.ToJSON());
                    IEnumerable<string> functionJSON = actions.Where(a => a.Type == ActionType.Function).Select(a => a.ToJSON());

                    var ysls = "{" +
                    @"""version"":2," +
                    $@"""commands"":[{string.Join(",", commandJSON)}]," +
                    $@"""functions"":[{string.Join(",", functionJSON)}]" +
                    "}";

                    File.WriteAllText(Path.Combine(outputDirectory.FullName, $"{compilation.AssemblyName ?? "NULL"}.ysls.json"), ysls);
                }
            }
        }
    }

    public interface ILogger
    {
        void Write(object obj);
        void WriteLine(object obj);
        void WriteException(System.Exception ex, string message = null);

        void Inc();
        void Dec();
        void SetDepth(int depth);
    }

    public class CmdLogger: ILogger
    {
        public void Write(object obj)
        {
            WriteLine(obj);
        }

        public void WriteLine(object obj)
        {
            var tabs = new String('\t', depth);
            Log.PrintLine(tabs + obj.ToString());
        }

        public void WriteException(Exception ex, string message = null)
        {
            if (message == null)
            {
                Log.Error(ex.Message);
            }
            else
            {
                Log.Error(message);
            }
        }

        private int depth = 0;
        public void Inc()
        {
            depth += 1;
        }

        public void Dec()
        {
            depth -= 1;
            if (depth < 0)
            {
                depth = 0;
            }
        }

        public void SetDepth(int depth)
        {
            this.depth = depth;
            if (this.depth < 0)
            {
                this.depth = 0;
            }
        }
    }

    public class NullLogger : ILogger
    {
        public void Dispose() { }

        public void Write(object text) { }

        public void WriteLine(object text) { }

        public void WriteException(System.Exception ex, string? message = null) { }

        public void Inc(){}
        public void Dec(){}
        public void SetDepth(int depth) {}
    }
}
