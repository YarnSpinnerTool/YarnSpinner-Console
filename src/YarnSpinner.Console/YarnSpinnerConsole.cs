namespace YarnSpinnerConsole
{
    using System;
    using System.Collections.Generic;
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
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            IgnoreNullValues = true,
            Converters =
                {
                    new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase),
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
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The files to compile");
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
            }

            compileCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo, string, string, string>(CompileCommand.CompileFiles);

            var runCommand = new System.CommandLine.Command("run", "Runs Yarn scripts in an interactive manner");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "the files to run")
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
            }

            runCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], string, bool>(RunCommand.RunFiles);

            var upgradeCommand = new System.CommandLine.Command("upgrade", "Upgrades Yarn scripts from one version of the language to another. Files will be modified in-place.");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "the files to upgrade")
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
                Argument<FileInfo[]> inputArgument = new Argument<FileInfo[]>("input", "the file to print a parse tree from")
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
                Argument<FileInfo[]> inputArgument = new Argument<FileInfo[]>("input", "the file to print a token list from")
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
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The files to tag with line IDs");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                tagCommand.AddArgument(inputsArgument.ExistingOnly());

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory to write the newly tagged files (default: override the input files)");
                outputOption.AddAlias("--output-directory");
                tagCommand.AddOption(outputOption.ExistingOnly());
            }

            tagCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo>(TagCommand.TagFiles);

            var extractCommand = new Command("extract", "Extracts strings from the provided files");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "the files to extract strings from");
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
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "the yarn files to use for the graph");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                graphCommand.AddArgument(inputsArgument.ExistingOnly());

                var output = new Option<FileInfo>("-o", "File location for saving the graph. Defaults to a file named dialogue.dot in the current directory");
                output.AddAlias("--output");
                graphCommand.AddOption(output);
            }

            graphCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], FileInfo>(GraphExport.CreateGraph);

            // Create a root command with our subcommands
            var rootCommand = new RootCommand
            {
                runCommand,
                compileCommand,
                upgradeCommand,
                dumpTreeCommand,
                dumpTokensCommand,
                tagCommand,
                extractCommand,
                graphCommand,
            };

            rootCommand.Description = "Compiles, runs and analyses Yarn code.";

            // Don't provide a handler to rootCommand so that not giving a
            // subcommand results in an error

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        // compiles a given yarn story designed to be called by runners or
        // the generic compile command does no writing
        public static CompilationResult CompileProgram(FileInfo[] inputs)
        {
            // The list of all files and their associated compiled results
            var results = new List<(FileInfo file, Yarn.Program program, IDictionary<string, StringInfo> stringTable)>();

            var compilationJob = CompilationJob.CreateFromFiles(inputs.Select(fileInfo => fileInfo.FullName));

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
