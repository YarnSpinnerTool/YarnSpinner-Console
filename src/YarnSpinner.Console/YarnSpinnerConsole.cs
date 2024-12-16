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
            IgnoreNullValues = true,
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
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The .yarnproject file to compile, or a collection of .yarn files to compile.");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                compileCommand.AddArgument(inputsArgument.ExistingOnly());

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory (default: current directory)");
                outputOption.AddAlias("--output-directory");
                outputOption.Argument.SetDefaultValue(System.Environment.CurrentDirectory);
                compileCommand.AddOption(outputOption.ExistingOnly());

                var outputFilenameOption = new Option<string>("-n", "Output name to use for the files (default: Output)");
                outputFilenameOption.AddAlias("--output-name");
                outputFilenameOption.Argument.SetDefaultValue("Output");
                compileCommand.AddOption(outputFilenameOption);

                var outputStringTableOption = new Option<string>("-t", "Output string table filename (default: {name}-Lines.csv");
                outputStringTableOption.AddAlias("--output-string-table-name");
                compileCommand.AddOption(outputStringTableOption);

                var outputMetadataTableOption = new Option<string>("-m", "Output metadata table filename (default: {name}-Metadata.csv");
                outputMetadataTableOption.AddAlias("--output-metadata-table-name");
                compileCommand.AddOption(outputMetadataTableOption);

                var stdoutOption = new Option<bool>("--stdout", "Output machine-readable compilation result to stdout instead of to files");
                compileCommand.AddOption(stdoutOption);

                var allowPreviewFeaturesOption = new Option<bool>("-p", "Allow using in-development compiler features.");
                allowPreviewFeaturesOption.AddAlias("--allow-preview-features");
                allowPreviewFeaturesOption.Argument.SetDefaultValue(false);
                compileCommand.AddOption(allowPreviewFeaturesOption);
            }

            compileCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo, string, string, string, bool, bool>(CompileCommand.CompileFiles);

            var listSourcesCommand = new System.CommandLine.Command("list-sources", "Lists Yarn sources for a Yarn project.");
            {
                Argument<FileInfo> yarnprojectArgument = new Argument<FileInfo>("yarnproject", "The .yarnproject file to list .yarn sources for.");
                yarnprojectArgument.Arity = ArgumentArity.ExactlyOne;
                listSourcesCommand.AddArgument(yarnprojectArgument.ExistingOnly());
            }

            listSourcesCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo>(ListSourcesCommand.ListSources);

            var runCommand = new System.CommandLine.Command("run", "Runs Yarn scripts in an interactive manner");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The .yarnproject file to run, or a collection of .yarn files to run. One of the specified files must contain the start node.")
                {
                    Arity = ArgumentArity.OneOrMore,
                };

                runCommand.AddArgument(inputsArgument.ExistingOnly());

                var startNodeOption = new Option<string>("-s", "Name of the node to start running");
                startNodeOption.AddAlias("--start-node");
                startNodeOption.Argument.SetDefaultValue("Start");
                startNodeOption.Argument.Arity = ArgumentArity.ExactlyOne;
                runCommand.AddOption(startNodeOption);

                var autoAdvance = new Option<bool>("--auto-advance", "Auto-advance regular dialogue lines");
                autoAdvance.AddAlias("-a");
                runCommand.AddOption(autoAdvance);

                var allowPreviewFeaturesOption = new Option<bool>("-p", "Allow using in-development compiler features.");
                allowPreviewFeaturesOption.AddAlias("--allow-preview-features");
                allowPreviewFeaturesOption.Argument.SetDefaultValue(false);
                runCommand.AddOption(allowPreviewFeaturesOption);
            }

            runCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], string, bool, bool>(RunCommand.RunFiles);

            var upgradeCommand = new System.CommandLine.Command("upgrade", "Upgrades Yarn scripts from one version of the language to another. Files will be modified in-place.");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The .yarnproject file to upgrade the files of, or a collection of .yarn files to upgrade.")
                {
                    Arity = ArgumentArity.OneOrMore,
                };

                upgradeCommand.AddArgument(inputsArgument.ExistingOnly());

                var upgradeTypeOption = new Option<int>("-t", "Upgrade type");
                upgradeTypeOption.AddAlias("--upgrade-type");
                upgradeTypeOption.Argument.SetDefaultValue(1);
                upgradeTypeOption.FromAmong("1");
                upgradeTypeOption.Argument.Arity = ArgumentArity.ExactlyOne;
                upgradeCommand.AddOption(upgradeTypeOption);
            }

            upgradeCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], Yarn.Compiler.Upgrader.UpgradeType>(UpgradeCommand.UpgradeFiles);

            var dumpTreeCommand = new System.CommandLine.Command("print-tree", "Parses a Yarn script and produces a human-readable syntax tree.");
            {
                Argument<FileInfo[]> inputArgument = new Argument<FileInfo[]>("input", "The .yarnproject file indicating files to generate a parse tree from, or a collection of .yarn files.")
                {
                    Arity = ArgumentArity.OneOrMore,
                };
                dumpTreeCommand.AddArgument(inputArgument.ExistingOnly());

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory (default: current directory)");
                outputOption.AddAlias("--output-directory");
                outputOption.Argument.SetDefaultValue(System.Environment.CurrentDirectory);
                dumpTreeCommand.AddOption(outputOption.ExistingOnly());

                var jsonOption = new Option<bool>("-j", "Output as JSON (default: false)");
                jsonOption.AddAlias("--json");
                jsonOption.Argument.SetDefaultValue(false);
                dumpTreeCommand.AddOption(jsonOption);
            }

            dumpTreeCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo, bool>(DumpTreeCommand.DumpTree);

            var dumpTokensCommand = new System.CommandLine.Command("print-tokens", "Parses a Yarn script and produces list of parsed tokens.");
            {
                Argument<FileInfo[]> inputArgument = new Argument<FileInfo[]>("input", "The .yarnproject file to generate a token list from, or a collection of .yarn files.")
                {
                    Arity = ArgumentArity.OneOrMore,
                };
                dumpTokensCommand.AddArgument(inputArgument.ExistingOnly());

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory (default: current directory)");
                outputOption.AddAlias("--output-directory");
                outputOption.Argument.SetDefaultValue(System.Environment.CurrentDirectory);
                dumpTokensCommand.AddOption(outputOption.ExistingOnly());

                var jsonOption = new Option<bool>("-j", "Output as JSON (default: false)");
                jsonOption.AddAlias("--json");
                jsonOption.Argument.SetDefaultValue(false);
                dumpTokensCommand.AddOption(jsonOption);
            }

            dumpTokensCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo, bool>(DumpTokensCommand.DumpTokens);

            var tagCommand = new Command("tag", "Tags a Yarn script with localisation line IDs");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The .yarnproject file indicating files to add tags to, or a collection of .yarn files");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                tagCommand.AddArgument(inputsArgument.ExistingOnly());

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory to write the newly tagged files (default: override the input files)");
                outputOption.AddAlias("--output-directory");
                tagCommand.AddOption(outputOption.ExistingOnly());
            }

            tagCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo>(TagCommand.TagFiles);

            var extractCommand = new Command("extract", "Extracts strings from the provided files");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The .yarnproject file indicating Yarn files to extract strings from, or a collection of .yarn files.");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                extractCommand.AddArgument(inputsArgument.ExistingOnly());

                var exportFormat = new Option<string>("--format", "The export file format for the extracted strings, defaults to csv").FromAmong("csv", "xlsx");
                exportFormat.AddAlias("-f");
                exportFormat.Argument.SetDefaultValue("csv");
                extractCommand.AddOption(exportFormat);

                var columns = new Option<string[]>("--columns", "The desired columns in the exported table of strings.");
                columns.Argument.SetDefaultValue(new string[] { "character", "text", "id", });
                extractCommand.AddOption(columns);

                var defaultCharacterName = new Option<string>("--default-name", "The default character name to use. Defaults to none.");
                defaultCharacterName.Argument.SetDefaultValue(null);
                extractCommand.AddOption(defaultCharacterName);

                var output = new Option<FileInfo>("-o", "File location for saving the extracted strings. Defaults to a file named lines in the current directory");
                output.AddAlias("--output");
                extractCommand.AddOption(output);
            }

            extractCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], string, string[], FileInfo, string>(StringExtractCommand.ExtractStrings);

            var graphCommand = new Command("graph", "Exports a graph view of the dialogue");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The .yarnproject file indicating Yarn files to export a graph of, or a collection of .yarn files.");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                graphCommand.AddArgument(inputsArgument.ExistingOnly());

                var output = new Option<FileInfo>("-o", "File location for saving the graph. Defaults to a file named dialogue in the current directory");
                output.AddAlias("--output");
                graphCommand.AddOption(output);

                var exportFormat = new Option<string>("--format", "The graph format, defaults to dot").FromAmong("dot", "mermaid");
                exportFormat.AddAlias("-f");
                exportFormat.Argument.SetDefaultValue("dot");
                graphCommand.AddOption(exportFormat);

                var clusterOption = new Option<bool>("-c", "Generate a graph with clustering subgraphs (default: false)");
                clusterOption.AddAlias("--clustering");
                clusterOption.Argument.SetDefaultValue(false);
                graphCommand.AddOption(clusterOption);
            }

            graphCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], FileInfo, string, bool>(GraphExport.CreateGraph);

            var versionCommand = new Command("version", "Show version info");
            versionCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create(() =>
            {
                Console.WriteLine($"ysc version " + typeof(YarnSpinnerConsole).Assembly.GetName().Version);

                Console.WriteLine($"YarnSpinner.dll version " + typeof(Yarn.Dialogue).Assembly.GetName().Version);
                Console.WriteLine($"YarnSpinner.Compiler.dll version " + typeof(Yarn.Compiler.Compiler).Assembly.GetName().Version);
            });

            var browsebinaryCommand = new Command("browse-binary", "Browses some of the common data inside of a compiled yarn program");
            {
                Argument<FileInfo> compiledInput = new Argument<FileInfo>("compiledInput", "The .yarnc file you want to browse");
                compiledInput.Arity = ArgumentArity.ExactlyOne;
                browsebinaryCommand.AddArgument(compiledInput.ExistingOnly());
            }
            browsebinaryCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo>(BrowseCompiledBinaryCommand.BrowseBinary);

            var createProjectFileCommand = new Command("create-proj", "Creates a yarn project file with default values");
            {
                Argument<string> projectNameInput = new Argument<string>("project-name", "The project name");
                projectNameInput.Arity = ArgumentArity.ExactlyOne;
                createProjectFileCommand.AddArgument(projectNameInput);

                var unityExclude = new Option<bool>("-u", "Set the excludeFiles value to ignore folders with trailing ~ (default: false)");
                unityExclude.AddAlias("--unity-exclusion");
                unityExclude.Argument.SetDefaultValue(false);
                createProjectFileCommand.AddOption(unityExclude);
            }
            createProjectFileCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<string, bool>(CreateProjectFileCommand.CreateProjFile);

            // Create a root command with our subcommands
            var rootCommand = new RootCommand
            {
                runCommand,
                compileCommand,
                listSourcesCommand,
                upgradeCommand,
                dumpTreeCommand,
                dumpTokensCommand,
                tagCommand,
                extractCommand,
                graphCommand,
                browsebinaryCommand,
                createProjectFileCommand,
                versionCommand,            };

            rootCommand.Description = "Compiles, runs and analyses Yarn code.";

            // Don't provide a handler to rootCommand so that not giving a
            // subcommand results in an error

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        // Compiles a given Yarn story. Designed to be called by runners or the
        // generic compile command. Does no writing.
        public static CompilationResult CompileProgram(FileInfo[] inputs, bool allowPreviewFeatures)
        {
            // Given the list of files that we've received, figure out which
            // Yarn files to compile. (If we were given a Yarn Project, this
            // method will figure out which source files to use.)
            var compilationJob = CompileCommand.GetCompilationJob(inputs);

            // Declare the existence of 'visited' and 'visited_count'
            var visitedDecl = new DeclarationBuilder()
                .WithName("visited")
                .WithType(
                    new FunctionTypeBuilder()
                        .WithParameter(Yarn.BuiltinTypes.String)
                        .WithReturnType(Yarn.BuiltinTypes.Boolean)
                        .FunctionType)
                .Declaration;

            var visitedCountDecl = new DeclarationBuilder()
                .WithName("visited_count")
                .WithType(
                    new FunctionTypeBuilder()
                        .WithParameter(Yarn.BuiltinTypes.String)
                        .WithReturnType(Yarn.BuiltinTypes.Number)
                        .FunctionType)
                .Declaration;

            compilationJob.VariableDeclarations = (compilationJob.VariableDeclarations ?? Array.Empty<Declaration>()).Concat(new[] {
                visitedDecl,
                visitedCountDecl,
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
