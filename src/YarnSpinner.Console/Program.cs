using System;
using Yarn;
using Yarn.Compiler;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using CsvHelper;

namespace YarnSpinnerConsole
{
    static class Log 
    {
        public static void PrintLine(string prefix, ConsoleColor color, string text) 
        {
            Console.ResetColor();
            Console.ForegroundColor = color;
            Console.Write(prefix);
            Console.Write(": ");
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void Info(string text) 
        {
            PrintLine("💁‍♂️ INFO", ConsoleColor.Blue, text);
        }

        public static void Warn(string text) 
        {
            PrintLine("⚠️ WARNING", ConsoleColor.Yellow, text);
        }

        public static void Error(string text) 
        {
            PrintLine("🚨 ERROR", ConsoleColor.Red, text);
        }

        public static void Fatal(string text) 
        {
            PrintLine("🚨 ERROR", ConsoleColor.Red, text);
            Environment.Exit(1);
        }
    }

    class ConsoleApp
    {
        static int Main(string[] args)
        {
            var compileCommand = new System.CommandLine.Command("compile", "Compiles Yarn scripts."); 
            {

                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The files to compile");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                compileCommand.AddArgument(inputsArgument.ExistingOnly());

                compileCommand.AddOption(new Option<bool>("--merge", "Merge output into a single file (default: compile into separate files)"));

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory (default: current directory)");
                outputOption.AddAlias("--output-directory");
                outputOption.Argument.SetDefaultValue(System.Environment.CurrentDirectory);

                compileCommand.AddOption(outputOption.ExistingOnly());
            }

            compileCommand.Handler = CommandHandler.Create<FileInfo[], bool, DirectoryInfo>(CompileFiles);

            var runCommand = new System.CommandLine.Command("run", "Runs Yarn scripts in an interactive manner");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "the files to run");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                runCommand.AddArgument(inputsArgument.ExistingOnly());

                var startNodeOption = new Option<String>("-s", "Name of the node to start running");
                startNodeOption.AddAlias("--start-node");
                startNodeOption.Argument.SetDefaultValue("Start");
                startNodeOption.Argument.Arity = ArgumentArity.ExactlyOne;
                runCommand.AddOption(startNodeOption);

                var autoAdvance = new Option<bool>("--auto-advance", "Auto-advance regular dialogue lines");
                autoAdvance.AddAlias("-a");
                runCommand.AddOption(autoAdvance);
            }
            runCommand.Handler = CommandHandler.Create<FileInfo[], string, bool>(RunFiles);

            // Create a root command with our two subcommands
            var rootCommand = new RootCommand();
            rootCommand.Add(runCommand);
            rootCommand.Add(compileCommand);

            rootCommand.Description = "Compiles, runs and analyses Yarn code.";

            // Don't provide a handler to rootCommand so that not giving a
            // subcommand results in an error

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        private static void RunFiles(FileInfo[] inputs, string StartNode, bool autoAdvance)
        {
            // this will be a new interactive command for running yarn stories
            // will compile and then run them
            var results = CompileProgram(inputs, true);

            string TextForLine(string lineID)
            {
                var text = results[0].stringTable[lineID];

                return text.text ?? lineID;
            }

            if (results.Count == 1)
            {
                var program = results[0].program;

                if (program.Nodes.ContainsKey(StartNode))
                {
                    var storage = new Yarn.MemoryVariableStore();
                    var dialogue = new Yarn.Dialogue(storage);
                    dialogue.LogDebugMessage = (m) => Log.Info(m);
                    dialogue.LogErrorMessage = (m) => Log.Error(m);

                    dialogue.SetProgram(program);
                    dialogue.SetNode(StartNode);

                    Dialogue.CommandHandler commandHandler = (Yarn.Command command) => 
                    {
                        Log.Info($"Received command: {command.Text}");

                        return Yarn.Dialogue.HandlerExecutionType.ContinueExecution;
                    };

                    Dialogue.LineHandler lineHandler = (Yarn.Line line) =>
                    {
                        if (autoAdvance)
                        {
                            Console.WriteLine(TextForLine(line.ID));
                        }
                        else
                        {
                            Console.Write(TextForLine(line.ID));
                            Console.ReadLine();
                        }

                        return Yarn.Dialogue.HandlerExecutionType.ContinueExecution;
                    };

                    Dialogue.OptionsHandler optionsHandler = (Yarn.OptionSet options) => 
                    {
                        Log.Info($"Received option group");

                        int count = 0;
                        foreach (var option in options.Options)
                        {
                            Console.WriteLine($"{count}: {TextForLine(option.Line.ID)}");
                            count += 1;
                        }
                        Console.WriteLine("Select an option to continue");

                        int number;
                        while (Int32.TryParse(Console.ReadLine(), out number) == false)
                        {
                            Console.WriteLine($"Select an option between 0 and {options.Options.Length - 1} to continue");
                        }

                        // rather than just trapping every possibility we just mash it into shape
                        number = number % options.Options.Length;
                        if (number < 0)
                        {
                            number *= -1;
                        }
                        Log.Info($"Selecting option {number}");

                        dialogue.SetSelectedOption(number);
                    };

                    Dialogue.NodeCompleteHandler nodeCompleteHandler = (string completedNodeName) => 
                    {
                        Log.Info($"Completed '{completedNodeName}' node");
                        return Yarn.Dialogue.HandlerExecutionType.ContinueExecution;
                    };

                    Dialogue.DialogueCompleteHandler dialogueCompleteHandler = () => { Log.Info("Dialogue Complete"); };

                    dialogue.lineHandler = lineHandler;
                    dialogue.commandHandler = commandHandler;
                    dialogue.optionsHandler = optionsHandler;
                    dialogue.nodeCompleteHandler = nodeCompleteHandler;
                    dialogue.dialogueCompleteHandler = dialogueCompleteHandler;

                    // libraries can't be customised in YarnSpinner 1.x.y
                    // this means any undefined function will throw an exception we can't handle
                    // so the best we can do is capture any undefined function exceptions
                    // future versions ideally will allow us to capture undefined funcs
                    // and we will then allow the user to determine what should happen
                    // for now though, behold my terrible hack
                    try
                    {
                        do 
                        {
                            dialogue.Continue();                        
                        }
                        while (dialogue.IsActive);
                    }
                    catch (InvalidOperationException ex)
                    {
                        Log.Fatal($"Undefined function encountered: {ex.Message}");
                    }
                }
                else
                {
                    Log.Error($"Unable to locate a node named {StartNode} to begin. Aborting");
                }
            }
        }

        // compiles a given yarn story
        // designed to be called by runners or the generic compile command
        // does no writing
        private static List<(Program program, IDictionary<string, StringInfo> stringTable)> CompileProgram(FileInfo[] inputs, bool merge)
        {
            // The list of files that failed to compile
            var failures = new List<FileInfo>();

            // The list of all files and their associated compiled results
            var results = new List<(FileInfo file, Program program, IDictionary<string,StringInfo> stringTable)>();

            foreach (var file in inputs) 
            {
                Yarn.Program program;
                IDictionary<string,StringInfo> newStringTable;
                try 
                {
                    Compiler.CompileFile(file.FullName, out program, out newStringTable);
                } 
                catch (Exception e) 
                {
                    Log.Error($"{file.FullName}: {e.Message}");
                    failures.Add(file);
                    continue;
                }

                results.Add((file, program, newStringTable));
            }

            if (failures.Count > 0) 
            {
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("Aborting compile because the following files encountered an error:");
                foreach (var failure in failures) 
                {
                    errorBuilder.AppendLine($" - {failure.Name}");
                }
                Log.Error(errorBuilder.ToString());
                Environment.Exit(1);
                return null;
            }

            var output = new List<(Program program, IDictionary<string, StringInfo> stringTable)>();
            if (merge) 
            {
                var mergedResult = MergeResults(results);

                output.Add((mergedResult.Item1, mergedResult.Item2));                         
            }
            else 
            {
                // We're wanting each compilation result separately
                foreach (var resultEntry in results) 
                {
                    output.Add((resultEntry.program, resultEntry.stringTable));
                }
            }

            return output;
        }

        private static void CompileFiles(FileInfo[] inputs, bool merge, DirectoryInfo outputDirectory)
        {
            var compiledResults = CompileProgram(inputs, merge);
            var zippedResults = compiledResults.Zip(inputs, (r, f) => new {result = r, file = f});
            
            foreach(var resultEntry in zippedResults)
            {
                WriteResult(
                    resultEntry.result.program,
                    resultEntry.result.stringTable,
                    outputDirectory,
                    resultEntry.file.Name ?? "output"
                );
            }
        }

        private static void WriteResult(Program program, IDictionary<string, StringInfo> stringTable, DirectoryInfo output, string fileName)
        {
            var programOutputPath = Path.Combine(output.FullName, fileName + ".yarnc");
            var stringTableOutputPath = Path.Combine(output.FullName, fileName + ".csv");

            using (var outStream = new FileStream(programOutputPath, FileMode.OpenOrCreate)) 
            using (var codedStream = new Google.Protobuf.CodedOutputStream(outStream)) {
                program.WriteTo(codedStream);                
            }

            Log.Info($"Wrote {programOutputPath}");
            
            using (var writer = new StreamWriter(stringTableOutputPath)) {
                // Use the invariant culture when writing the CSV
                var configuration = new CsvHelper.Configuration.Configuration(
                    System.Globalization.CultureInfo.InvariantCulture
                );

                var csv = new CsvHelper.CsvWriter(
                    writer, // write into this stream
                    configuration // use this configuration
                    );

                var lines = stringTable.Select(x => new {
                    id = x.Key, 
                    text=x.Value.text,
                    file=x.Value.fileName,
                    node=x.Value.nodeName,
                    lineNumber=x.Value.lineNumber
                });

                csv.WriteRecords(lines);
            }

            Log.Info($"Wrote {stringTableOutputPath}");
            
        }

        private static (Program, IDictionary<string, StringInfo>) MergeResults(List<(FileInfo file, Program program, IDictionary<string, StringInfo> stringTable)> result)
        {
            // Merge all of these programs into a single program
                var finalProgram = Program.Combine(result.Select(r => r.program).ToArray());

                // Get a collection of all string entries
                var allStringKeys = result.SelectMany(r => r.stringTable.AsEnumerable());

                // Get a collection of string IDs that we've seen more than
                // once             
                var duplicateStringKeys = allStringKeys
                                          .GroupBy(pair => pair.Key, pair => pair.Value)
                                          .Where(group => group.Count() > 1);

                foreach (var duplicateKey in duplicateStringKeys) {
                    // Create a collection of strings describing where this
                    // line id has been seen
                    var occurrences = duplicateKey
                        .SelectMany(info => $"{info.fileName}:{info.lineNumber}");
                    
                    // Log a warning about it
                    Log.Error($"Duplicate line ID {duplicateKey.Key} (occurs in {string.Join(", ", occurrences)})");
                }

                // Stop here if there are any duplicate keys because we
                // can't create a canonical, merged, unique string table
                if (duplicateStringKeys.Count() > 1) {
                    Environment.Exit(1);
                    return (null,null);
                }

                // Combine all of the keys into a single key
                var finalStringTable = allStringKeys.ToDictionary(entry => entry.Key, entry => entry.Value);

                return (finalProgram, finalStringTable);
        }
    }
}
