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
                    GenerateYSLSFilesForGodotGDScript(inputDirectory, outputDirectory);
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

        private static void GenerateYSLSFilesForGodotGDScript(DirectoryInfo inputDirectory, DirectoryInfo outputDirectory)
        {
            Log.Info($"Scanning GDScript files in {inputDirectory.FullName}");

            var gdFiles = inputDirectory.GetFiles("*.gd", SearchOption.AllDirectories);
            Log.Info($"Found {gdFiles.Length} .gd files");

            var commands = new List<string>();
            var functions = new List<string>();

            foreach (var file in gdFiles)
            {
                var lines = File.ReadAllLines(file.FullName);
                var relativePath = Path.GetRelativePath(inputDirectory.FullName, file.FullName);

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();

                    string? prefix = null;
                    bool isFunction = false;

                    if (line.StartsWith("func _yarn_command_"))
                    {
                        prefix = "_yarn_command_";
                    }
                    else if (line.StartsWith("func _yarn_function_"))
                    {
                        prefix = "_yarn_function_";
                        isFunction = true;
                    }

                    if (prefix == null) continue;

                    // Handle multi-line signatures by joining lines until we find ')'
                    var fullLine = line;
                    while (!fullLine.Contains(')') && i + 1 < lines.Length)
                    {
                        i++;
                        fullLine += " " + lines[i].Trim();
                    }

                    var parsed = ParseGDScriptAction(fullLine, prefix, isFunction, relativePath, i);
                    if (parsed != null)
                    {
                        (isFunction ? functions : commands).Add(parsed);
                    }
                }
            }

            if (commands.Count == 0 && functions.Count == 0)
            {
                Log.Info(" 😴 No Yarn commands or functions found in GDScript files");
                return;
            }

            var ysls = "{" +
                @"""version"":2," +
                $@"""commands"":[{string.Join(",", commands)}]," +
                $@"""functions"":[{string.Join(",", functions)}]" +
                "}";

            if (!outputDirectory.Exists)
            {
                outputDirectory.Create();
            }

            var outputPath = Path.Combine(outputDirectory.FullName, "GDScript.ysls.json");
            File.WriteAllText(outputPath, ysls);
            Log.Info($" 😎 Found {commands.Count} commands and {functions.Count} functions");
            Log.Info($" 😎 Wrote {outputPath}");
        }

        private static string? ParseGDScriptAction(string line, string prefix, bool isFunction, string fileName, int lineNumber)
        {
            var afterFunc = line.Substring("func ".Length);
            var parenStart = afterFunc.IndexOf('(');
            if (parenStart < 0) return null;

            var methodName = afterFunc.Substring(0, parenStart);
            var yarnName = methodName.Substring(prefix.Length);

            var parenEnd = afterFunc.IndexOf(')');
            if (parenEnd < 0) return null;

            var paramString = afterFunc.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
            var parameters = ParseGDScriptParams(paramString);

            // Extract return type from -> annotation
            string? returnTypeStr = null;
            var arrowIndex = afterFunc.IndexOf("->", parenEnd);
            if (arrowIndex >= 0)
            {
                returnTypeStr = afterFunc.Substring(arrowIndex + 2).Trim().TrimEnd(':').Trim();
            }

            var result = new Dictionary<string, object?>
            {
                ["yarnName"] = yarnName,
                ["definitionName"] = methodName,
                ["fileName"] = fileName,
                ["language"] = "gdscript",
                ["containsErrors"] = false,
                ["parameters"] = parameters,
                ["location"] = new Dictionary<string, object>
                {
                    ["start"] = new Dictionary<string, int> { ["line"] = lineNumber, ["character"] = 0 },
                    ["end"] = new Dictionary<string, int> { ["line"] = lineNumber, ["character"] = line.Length }
                }
            };

            if (isFunction)
            {
                var yarnType = returnTypeStr != null ? MapGDScriptTypeToYarn(returnTypeStr) : "any";
                result["return"] = new Dictionary<string, string> { ["type"] = yarnType };
            }
            else
            {
                result["async"] = returnTypeStr?.Equals("Signal", StringComparison.OrdinalIgnoreCase) == true;
            }

            return System.Text.Json.JsonSerializer.Serialize(result);
        }

        private static List<Dictionary<string, object?>> ParseGDScriptParams(string paramString)
        {
            var result = new List<Dictionary<string, object?>>();
            if (string.IsNullOrWhiteSpace(paramString)) return result;

            var parts = paramString.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                string name;
                string type = "any";
                string? defaultValue = null;

                // Check for default value: param := value or param = value
                var eqIndex = trimmed.IndexOf('=');
                string beforeDefault = trimmed;
                if (eqIndex >= 0)
                {
                    // Handle := (typed default) and = (untyped default)
                    if (eqIndex > 0 && trimmed[eqIndex - 1] == ':')
                    {
                        beforeDefault = trimmed.Substring(0, eqIndex - 1).Trim();
                    }
                    else
                    {
                        beforeDefault = trimmed.Substring(0, eqIndex).Trim();
                    }
                    defaultValue = trimmed.Substring(eqIndex + 1).Trim();
                }

                // Check for type annotation: param: Type
                var colonIndex = beforeDefault.IndexOf(':');
                if (colonIndex >= 0)
                {
                    name = beforeDefault.Substring(0, colonIndex).Trim();
                    var gdType = beforeDefault.Substring(colonIndex + 1).Trim();
                    type = MapGDScriptTypeToYarn(gdType);
                }
                else
                {
                    name = beforeDefault;
                }

                var param = new Dictionary<string, object?> { ["name"] = name, ["type"] = type };
                if (defaultValue != null)
                {
                    param["defaultValue"] = defaultValue;
                }
                result.Add(param);
            }

            return result;
        }

        private static string MapGDScriptTypeToYarn(string gdType)
        {
            return gdType.ToLowerInvariant() switch
            {
                "string" or "stringname" => "string",
                "int" or "float" => "number",
                "bool" => "bool",
                _ => "any",
            };
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
