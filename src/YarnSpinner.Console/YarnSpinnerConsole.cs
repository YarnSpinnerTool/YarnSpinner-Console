namespace YarnSpinnerConsole
{
    using System;
    using System.CommandLine;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Yarn.Compiler;

    /// <summary>
    /// Provides the entry point to the ysc command.
    /// </summary>
    public class YarnSpinnerConsole
    {
        public static JsonSerializerOptions JsonSerializationOptions => new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            },
        };

        /// <summary>
        /// The entry point for the app.
        /// </summary>
        /// <param name="args">The list of arguments received by the application.</param>
        /// <returns>The return code for the application.</returns>
        public static int Main(string[] args)
        {
            var compileCommand = new System.CommandLine.Command("compile", "Compiles Yarn scripts.");
            {
                var inputsArgument = new Argument<FileInfo[]>("inputs")
                {
                    Description = "The .yarnproject file to compile, or a collection of .yarn files to compile.",
                    Arity = ArgumentArity.OneOrMore
                };
                compileCommand.Add(inputsArgument.AcceptExistingOnly());

                var outputOption = new Option<DirectoryInfo>("--output-directory", "-o")
                {
                    Description = "Output directory (default: current directory)",
                    DefaultValueFactory = parseResult => new DirectoryInfo(System.Environment.CurrentDirectory),
                };
                compileCommand.Add(outputOption.AcceptExistingOnly());

                var outputFilenameOption = new Option<string>("--output-name", "-n")
                {
                    Description = "Output name to use for the files (default: Output)",
                    DefaultValueFactory = parseResult => "Output",
                };
                compileCommand.Add(outputFilenameOption);

                var outputStringTableOption = new Option<string>("--output-string-table-name", "-t")
                {
                    Description = "Output string table filename (default: {name}-Lines.csv",
                };
                compileCommand.Add(outputStringTableOption);

                var outputMetadataTableOption = new Option<string>("--output-metadata-table-name","-m")
                {
                    Description = "Output metadata table filename (default: {name}-Metadata.csv",
                };
                compileCommand.Add(outputMetadataTableOption);

                var stdoutOption = new Option<bool>("--stdout")
                {
                    Description = "Output machine-readable compilation result to stdout instead of to files",
                };
                compileCommand.Add(stdoutOption);

                compileCommand.SetAction(parseResult => CompileCommand.CompileFiles(
                    parseResult.GetValue(inputsArgument),
                    parseResult.GetValue(outputOption),
                    parseResult.GetValue(outputFilenameOption),
                    parseResult.GetValue(outputStringTableOption),
                    parseResult.GetValue(outputMetadataTableOption),
                    parseResult.GetValue(stdoutOption)
                ));
            }

            var listSourcesCommand = new System.CommandLine.Command("list-sources", "Lists Yarn sources for a Yarn project.");
            {
                var yarnprojectArgument = new Argument<FileInfo>("yarnproject")
                {
                    Description = "The .yarnproject file to list .yarn sources for.",
                    Arity = ArgumentArity.ExactlyOne,
                };
                listSourcesCommand.Add(yarnprojectArgument);

                listSourcesCommand.SetAction(parseResult => ListSourcesCommand.ListSources(
                    parseResult.GetValue(yarnprojectArgument)
                ));
            }

            var runCommand = new System.CommandLine.Command("run", "Runs Yarn scripts in an interactive manner");
            {
                var inputsArgument = new Argument<FileInfo[]>("inputs")
                {
                    Description = "The .yarnproject file to run, or a collection of .yarn files to run. One of the specified files must contain the start node.",
                    Arity = ArgumentArity.OneOrMore,
                };
                runCommand.Add(inputsArgument.AcceptExistingOnly());

                var startNodeOption = new Option<string>("--start-node","-s")
                {
                    Description = "Name of the node to start running",
                    Arity = ArgumentArity.ExactlyOne,
                    DefaultValueFactory = parseResult => "Start",
                };
                runCommand.Add(startNodeOption);

                var autoAdvance = new Option<bool>("--auto-advance", "-a")
                {
                    Description = "Auto-advance regular dialogue lines",
                };
                runCommand.Add(autoAdvance);

                var allowPreviewFeaturesOption = new Option<bool>("--allow-preview-features", "-p")
                {
                    Description = "Allow using in-development compiler features.",
                    DefaultValueFactory = parseResult => false,
                };
                runCommand.Add(allowPreviewFeaturesOption);

                runCommand.SetAction(parseResult => RunCommand.RunFiles(
                    parseResult.GetValue(inputsArgument),
                    parseResult.GetValue(startNodeOption),
                    parseResult.GetValue(autoAdvance),
                    parseResult.GetValue(allowPreviewFeaturesOption)
                ));
            }

            var dumpTreeCommand = new System.CommandLine.Command("print-tree", "Parses a Yarn script and produces a human-readable syntax tree.");
            {
                var inputArgument = new Argument<FileInfo[]>("input")
                {
                    Description = "The .yarnproject file indicating files to generate a parse tree from, or a collection of .yarn files.",
                    Arity = ArgumentArity.OneOrMore,
                };
                dumpTreeCommand.Add(inputArgument.AcceptExistingOnly());

                var outputOption = new Option<DirectoryInfo>("--output-directory", "-o")
                {
                    Description = "Output directory (default: current directory)",
                    DefaultValueFactory = parseResult => new DirectoryInfo(System.Environment.CurrentDirectory),
                };
                dumpTreeCommand.Add(outputOption.AcceptExistingOnly());

                var jsonOption = new Option<bool>("--json", "-j")
                {
                    Description = "Output as JSON (default: false)",
                    DefaultValueFactory = parseResult => false,
                };
                dumpTreeCommand.Add(jsonOption);

                dumpTreeCommand.SetAction(parseResult => DumpTreeCommand.DumpTree(
                    parseResult.GetValue(inputArgument),
                    parseResult.GetValue(outputOption),
                    parseResult.GetValue(jsonOption)
                ));
            }

            var dumpTokensCommand = new System.CommandLine.Command("print-tokens", "Parses a Yarn script and produces list of parsed tokens.");
            {
                var inputArgument = new Argument<FileInfo[]>("input")
                {
                    Description = "The .yarnproject file to generate a token list from, or a collection of .yarn files.",
                    Arity = ArgumentArity.OneOrMore,
                };
                dumpTokensCommand.Add(inputArgument.AcceptExistingOnly());

                var outputOption = new Option<DirectoryInfo>("--output-directory", "-o")
                {
                    Description = "Output directory (default: current directory)",
                    DefaultValueFactory = parseResult => new DirectoryInfo(System.Environment.CurrentDirectory)
                };
                dumpTokensCommand.Add(outputOption.AcceptExistingOnly());

                var jsonOption = new Option<bool>("--json", "-j")
                {
                    Description = "Output as JSON (default: false)",
                    DefaultValueFactory = parseResult => false,
                };
                dumpTokensCommand.Add(jsonOption);

                dumpTokensCommand.SetAction(parseResult => DumpTokensCommand.DumpTokens(
                    parseResult.GetValue(inputArgument),
                    parseResult.GetValue(outputOption),
                    parseResult.GetValue(jsonOption)
                ));
            }

            var tagCommand = new Command("tag", "Tags a Yarn script with localisation line IDs");
            {
                var inputsArgument = new Argument<FileInfo[]>("inputs")
                {
                    Description = "The .yarnproject file indicating files to add tags to, or a collection of .yarn files",
                    Arity = ArgumentArity.OneOrMore,
                };
                tagCommand.Add(inputsArgument.AcceptExistingOnly());

                var outputOption = new Option<DirectoryInfo>("--output-directory", "-o")
                {
                    Description = "Output directory to write the newly tagged files (default: override the input files)"
                };
                tagCommand.Add(outputOption.AcceptExistingOnly());
                
                tagCommand.SetAction(parseResult => TagCommand.TagFiles(
                    parseResult.GetValue(inputsArgument),
                    parseResult.GetValue(outputOption)
                ));
            }

            var extractCommand = new Command("extract", "Extracts strings from the provided files");
            {
                var inputsArgument = new Argument<FileInfo[]>("inputs")
                {
                    Description = "The .yarnproject file indicating Yarn files to extract strings from, or a collection of .yarn files.",
                    Arity = ArgumentArity.OneOrMore,
                };
                extractCommand.Add(inputsArgument.AcceptExistingOnly());

                var exportFormat = new Option<string>("--format", "-f")
                {
                    Description = "The export file format for the extracted strings, defaults to csv",
                    DefaultValueFactory = parseResult => "csv"
                };
                extractCommand.Add(exportFormat.AcceptOnlyFromAmong("csv", "xlsx"));

                var columns = new Option<string[]>("--columns")
                {
                    Description = "The desired columns in the exported table of strings.",
                    DefaultValueFactory = parseResult => new string[] { "character", "text", "id", },
                };
                extractCommand.Add(columns);

                var defaultCharacterName = new Option<string>("--default-name")
                {
                    Description = "The default character name to use. Defaults to none.",
                };
                extractCommand.Add(defaultCharacterName);

                var output = new Option<FileInfo>("--output", "-o")
                {
                    Description = "File location for saving the extracted strings. Defaults to a file named lines in the current directory"
                };
                extractCommand.Add(output);

                extractCommand.SetAction(parseResult => StringExtractCommand.ExtractStrings(
                    parseResult.GetValue(inputsArgument),
                    parseResult.GetValue(exportFormat),
                    parseResult.GetValue(columns),
                    parseResult.GetValue(output),
                    parseResult.GetValue(defaultCharacterName)
                ));
            }

            var graphCommand = new Command("graph", "Exports a graph view of the dialogue");
            {
                var inputsArgument = new Argument<FileInfo[]>("inputs")
                {
                    Description = "The .yarnproject file indicating Yarn files to export a graph of, or a collection of .yarn files.",
                    Arity = ArgumentArity.OneOrMore
                };
                graphCommand.Add(inputsArgument.AcceptExistingOnly());

                var output = new Option<FileInfo>("--output", "-o")
                {
                    Description = "File location for saving the graph. Defaults to a file named dialogue in the current directory",
                };
                graphCommand.Add(output);

                var exportFormat = new Option<string>("--format", "-f")
                {
                    Description = "The graph format, defaults to dot",
                    DefaultValueFactory = parseResult => "dot"
                };
                graphCommand.Add(exportFormat.AcceptOnlyFromAmong("dot", "mermaid"));

                var clusterOption = new Option<bool>("--clustering", "-c")
                {
                    Description = "Generate a graph with clustering subgraphs (default: false)",
                    DefaultValueFactory = parseResult => false
                };
                graphCommand.Add(clusterOption);

                graphCommand.SetAction(parseResult => GraphExport.CreateGraph(
                    parseResult.GetValue(inputsArgument),
                    parseResult.GetValue(output),
                    parseResult.GetValue(exportFormat),
                    parseResult.GetValue(clusterOption)
                ));
            }

            var versionCommand = new Command("version", "Show version info");
            versionCommand.SetAction(parseResult =>
            {
                Console.WriteLine($"ysc version " + typeof(YarnSpinnerConsole).Assembly.GetName().Version);
                Console.WriteLine($"YarnSpinner.dll version " + typeof(Yarn.Dialogue).Assembly.GetName().Version);
                Console.WriteLine($"YarnSpinner.Compiler.dll version " + typeof(Yarn.Compiler.Compiler).Assembly.GetName().Version);
            });

            var browsebinaryCommand = new Command("browse-binary", "Browses some of the common data inside of a compiled yarn program");
            {
                var compiledInput = new Argument<FileInfo>("compiledInput")
                {
                    Description = "The .yarnc file you want to browse",
                    Arity = ArgumentArity.ExactlyOne
                };
                browsebinaryCommand.Add(compiledInput);

                browsebinaryCommand.SetAction(parseResult => BrowseCompiledBinaryCommand.BrowseBinary(
                    parseResult.GetValue(compiledInput)
                ));
            }

            var createProjectFileCommand = new Command("create-proj", "Creates a yarn project file with default values");
            {
                var projectNameInput = new Argument<string>("project-name")
                {
                    Description = "The project name",
                    Arity = ArgumentArity.ExactlyOne
                };
                createProjectFileCommand.Add(projectNameInput);

                var unityExclude = new Option<bool>("--unity-exclusion", "-u")
                {
                    Description = "Set the excludeFiles value to ignore folders with trailing ~ (default: false)",
                    DefaultValueFactory = parseResult => false,
                };
                createProjectFileCommand.Add(unityExclude);

                createProjectFileCommand.SetAction(parseResult => CreateProjectFileCommand.CreateProjFile(
                    parseResult.GetValue(projectNameInput),
                    parseResult.GetValue(unityExclude)
                ));
            }

            var dumpCodeCommand = new Command("dump-code", "Compiles the specified input, and prints the disassembled code.");
            {
                var inputsArgument = new Argument<FileInfo[]>("inputs")
                {
                    Description = "The .yarnproject file to compile, or a collection of .yarn files to compile.",
                    Arity = ArgumentArity.OneOrMore
                };
                dumpCodeCommand.Add(inputsArgument.AcceptExistingOnly());

                var allowPreviewFeaturesOption = new Option<bool>("--allow-preview-features", "-p")
                {
                    Description = "Allow using in-development compiler features.",
                    DefaultValueFactory = parseResult => false,
                };
                dumpCodeCommand.Add(allowPreviewFeaturesOption);

                dumpCodeCommand.SetAction(parseResult => DumpCompiledCodeCommand.DumpCompiledCode(
                    parseResult.GetValue(inputsArgument),
                    parseResult.GetValue(allowPreviewFeaturesOption)
                ));
            }

            var generateDefinitionsCommand = new Command("generate-definitions", "Generates YSLS files for your project.");
            {
                var inputDirectory = new Argument<DirectoryInfo>("inputDirectory")
                {
                    Description = "The directory of the project containing your implementation files",
                };
                generateDefinitionsCommand.Add(inputDirectory.AcceptExistingOnly());

                var outputDirectory = new Option<DirectoryInfo>("--output-directory", "-o")
                {
                    Description = "Output directory (default: current directory)",
                    DefaultValueFactory = parseResult => new DirectoryInfo(System.Environment.CurrentDirectory)
                };
                generateDefinitionsCommand.Add(outputDirectory.AcceptExistingOnly());

                var projectType = new Option<string>("--project-type", "-e")
                {
                    Description = "The engine this project uses. Used to determine the engine specific variations of the generated YSLS file.",
                    DefaultValueFactory = parseResult => "Unity"
                };
                generateDefinitionsCommand.Add(projectType.AcceptOnlyFromAmong("Unity", "Godot-gd", "Godot-csharp", "Unreal"));

                generateDefinitionsCommand.SetAction(parseResult => GenerateDefinitionsCommand.GenerateDefinitions(
                    parseResult.GetValue(inputDirectory),
                    parseResult.GetValue(outputDirectory),
                    parseResult.GetValue(projectType)
                ));
            }

            // Create a root command with our subcommands
            var rootCommand = new RootCommand("Compiles, runs and analyses Yarn code.");
            rootCommand.Subcommands.Add(compileCommand);
            rootCommand.Subcommands.Add(listSourcesCommand);
            rootCommand.Subcommands.Add(runCommand);
            rootCommand.Subcommands.Add(dumpTreeCommand);
            rootCommand.Subcommands.Add(dumpTokensCommand);
            rootCommand.Subcommands.Add(tagCommand);
            rootCommand.Subcommands.Add(extractCommand);
            rootCommand.Subcommands.Add(graphCommand);
            rootCommand.Subcommands.Add(versionCommand);
            rootCommand.Subcommands.Add(browsebinaryCommand);
            rootCommand.Subcommands.Add(createProjectFileCommand);
            rootCommand.Subcommands.Add(dumpCodeCommand);
            rootCommand.Subcommands.Add(generateDefinitionsCommand);

            // Parse the incoming args and invoke the handler
            return rootCommand.Parse(args).Invoke();
        }

        // Compiles a given Yarn story. Designed to be called by runners or the
        // generic compile command. Does no writing.
        public static CompilationResult CompileProgram(FileInfo[] inputs)
        {
            // Given the list of files that we've received, figure out which
            // Yarn files to compile. (If we were given a Yarn Project, this
            // method will figure out which source files to use.)
            var compilationJob = CompileCommand.GetCompilationJob(inputs);

            // Declare the existence of functions that are not part of the static StandardLibrary but are declared in Dialogue
            var visitedDecl = new DeclarationBuilder()
                .WithName("visited")
                .WithType(
                    new FunctionTypeBuilder()
                        .WithParameter(Yarn.Types.String)
                        .WithReturnType(Yarn.Types.Boolean)
                        .FunctionType)
                .Declaration;

            var visitedCountDecl = new DeclarationBuilder()
                .WithName("visited_count")
                .WithType(
                    new FunctionTypeBuilder()
                        .WithParameter(Yarn.Types.String)
                        .WithReturnType(Yarn.Types.Number)
                        .FunctionType)
                .Declaration;

            var hasAnyContent = new DeclarationBuilder()
                .WithName("has_any_content")
                .WithType(
                    new FunctionTypeBuilder()
                        .WithParameter(Yarn.Types.String)
                        .WithReturnType(Yarn.Types.Boolean)
                        .FunctionType)
                        .Declaration;

            compilationJob.Declarations = (compilationJob.Declarations ?? Array.Empty<Declaration>()).Concat(new[] {
                visitedDecl,
                visitedCountDecl,
                hasAnyContent,
            });

            CompilationResult compilationResult;

            try
            {
                compilationResult = Compiler.Compile(compilationJob);
            }
            catch (Exception e)
            {
                var errorBuilder = new StringBuilder();

                errorBuilder.AppendLine("Failed to compile because of the following error:");
                errorBuilder.AppendLine(e.ToString());

                Log.Error(errorBuilder.ToString());
                Environment.Exit(1);

                // Environment.Exit will stop the program before here;
                // throw an exception so the compiler doesn't wonder why
                // we're not returning a value.
                throw new Exception();
            }

            return compilationResult;
        }
    }
}
