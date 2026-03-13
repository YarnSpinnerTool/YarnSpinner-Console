#nullable enable

namespace YarnSpinnerConsole
{
    using Microsoft.Build.Locator;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

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
                    Log.Error("At this stage ysc only supports generating YSLS files for csharp projects");
                    System.Environment.Exit(1);
                    break;
                case "Godot-csharp":
                    GenerateYSLSFilesForGodot(inputDirectory, outputDirectory);
                    break;
                case "Unreal":
                    Log.Error("At this stage ysc only supports generating YSLS files for csharp projects");
                    System.Environment.Exit(1);
                    break;
                default:
                    Log.Error($"\"{projectType}\" is an unsupported engine.");
                    System.Environment.Exit(1);
                    break;
            }
        }

        private static void GenerateYSLSFilesForCSharp(DirectoryInfo inputDirectory, DirectoryInfo outputDirectory, List<string> requiredAssemblies, List<string> assemblyPrefixesToIgnore, List<string> assemblyPrefixesToKeep)
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

            var solutions = inputDirectory.GetFiles("*.sln").Concat(inputDirectory.GetFiles("*.slnx")).ToArray();

            if (solutions.Length > 1)
            {
                Log.Error($"Multiple solution files found in {inputDirectory.FullName}");
                System.Environment.Exit(1);
            }
            if (solutions.Length == 0)
            {
                Log.Error($"No solution files found in {inputDirectory.FullName}");
                System.Environment.Exit(1);
            }

            var logger = new NullLogger();

            var tracker = new TimeTracker((message, phaseSeconds, totalSeconds) => Log.Info($" ⏰ {string.Format("{0:F2}", phaseSeconds),7}s {message} ({totalSeconds:F2}s total)"));

            tracker.StartPhase("Open solution");
            Log.Info("📋 Opening " + solutions.Single().FullName);

            var workspace = MSBuildWorkspace.Create();
            var progress = new Progress<ProjectLoadProgress>(p =>
            {
                if (p.Operation == ProjectLoadOperation.Resolve)
                {
                    Log.Info($"  👀 {p.FilePath}");
                }
            });
            var solution = workspace.OpenSolutionAsync(solutions.Single().FullName, progress).Result;

            tracker.StartPhase("Extract actions");

            Parallel.ForEach(solution.Projects, (project) =>
            {
                var projectTracker = new TimeTracker((message, phaseSeconds, totalSeconds) => Log.Info($" ⏰ {project.AssemblyName}: {string.Format("{0:F2}", phaseSeconds),7}s {message} ({totalSeconds:F2}s total)"));
                try
                {
                    projectTracker.StartPhase("Get compilation");
                    Log.Info("📋 Starting work on " + project.AssemblyName);
                    var compilation = project.WithParseOptions(CSharpParseOptions.Default).GetCompilationAsync().Result as CSharpCompilation;

                    projectTracker.StartPhase("Check compilation");

                    if (compilation == null)
                    {
                        projectTracker.Stop();
                        Log.Error(" 🤬 Failed to get a compilation for " + project.Solution);
                        return;
                    }

                    var assemblyName = compilation.AssemblyName ?? "NULL";

                    if (!compilation.ReferencedAssemblyNames.Any(a => requiredAssemblies.Contains(a.Name)))
                    {
                        projectTracker.Stop();
                        Log.Info($" 🫥 Assembly {assemblyName} doesn't reference Yarn Spinner, skipping");
                        return;
                    }

                    if (assemblyPrefixesToIgnore.Any(prefix => assemblyName.StartsWith(prefix)) && !assemblyPrefixesToKeep.Any(prefix => assemblyName.StartsWith(prefix)))
                    {
                        projectTracker.Stop();
                        Log.Info($" 🫥 {assemblyName} references an ignored assembly (and doesn't reference a kept assembly), skipping");
                        return;
                    }

                    projectTracker.StartPhase("Get actions");

                    List<Action> actions = new List<Action>();
                    foreach (var tree in compilation.SyntaxTrees)
                    {
                        actions.AddRange(Analyser.GetActions(compilation, tree, logger));
                    }

                    if (actions.Count > 0)
                    {
                        projectTracker.StartPhase("Validate actions");
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

                        var outputPath = Path.Combine(outputDirectory.FullName, $"{assemblyName}.ysls.json");
                        File.WriteAllText(outputPath, ysls);
                        projectTracker.Stop();
                        Log.Info($" 😎 Wrote {outputPath}");
                    }
                    else
                    {
                        projectTracker.Stop();
                        Log.Info($" 😴 No actions found in {assemblyName}, skipping ysls.json generation");
                    }
                }
                finally
                {
                    projectTracker.Stop();
                }

            });

            tracker.Stop();

        }

        private static void GenerateYSLSFilesForUnity(DirectoryInfo inputDirectory, DirectoryInfo outputDirectory)
        {
            // we don't want to generate YSLS file for anything in the built in assemblies
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
            // and we need to have the Yarn Spinner dll referenced
            var required = new List<string>()
            {
                "YarnSpinner.Unity",
            };
            GenerateYSLSFilesForCSharp(inputDirectory, outputDirectory, required, prefixesToIgnore, prefixesToKeep);
        }

        private static void GenerateYSLSFilesForGodot(DirectoryInfo inputDirectory, DirectoryInfo outputDirectory)
        {
            // we don't want to knock out any specific assemblies for godot
            List<string> emptyList = new List<string>();
            // but we do still need to reference the Yarn Spinner DLLs
            var required = new List<string>()
            {
                "YarnSpinner",
                "YarnSpinner.Compiler",
            };
            GenerateYSLSFilesForCSharp(inputDirectory, outputDirectory, required, emptyList, emptyList);
        }
    }

    public interface ILogger
    {
        void Write(object obj);
        void WriteLine(object obj);
        void WriteException(System.Exception ex, string? message = null);

        void Inc();
        void Dec();
        void SetDepth(int depth);
    }

    public class CmdLogger : ILogger
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

        public void WriteException(Exception ex, string? message = null)
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

        public void Inc() { }
        public void Dec() { }
        public void SetDepth(int depth) { }
    }
}
