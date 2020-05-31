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
    static class Log {
        public static void PrintLine(string prefix, ConsoleColor color, string text) {
            Console.ResetColor();
            Console.ForegroundColor = color;
            Console.Write(prefix);
            Console.Write(": ");
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void Info(string text) {
            PrintLine("💁‍♂️ INFO", ConsoleColor.Blue, text);
        }

        public static void Warn(string text) {
            PrintLine("⚠️ WARNING", ConsoleColor.Yellow, text);
        }

        public static void Error(string text) {
            PrintLine("🚨 ERROR", ConsoleColor.Red, text);
        }

        public static void Fatal(string text) {
            PrintLine("🚨 ERROR", ConsoleColor.Red, text);
            Environment.Exit(1);
        }
    }

    class ConsoleApp
    {
        static int Main(string[] args)
        {


            var runCommand = new System.CommandLine.Command("compile", "Compiles Yarn scripts."); 
            {

                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The files to compile");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                runCommand.AddArgument(inputsArgument.ExistingOnly());

                runCommand.AddOption(new Option<bool>("--merge", "Merge output into a single file (default: compile into separate files)"));

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory (default: current directory)");
                outputOption.AddAlias("--output-directory");
                outputOption.Argument.SetDefaultValue(System.Environment.CurrentDirectory);

                runCommand.AddOption(outputOption.ExistingOnly());
            }

            runCommand.Handler = CommandHandler.Create<FileInfo[], bool, DirectoryInfo>(CompileFiles);

            // Create a root command with some options
            var rootCommand = new RootCommand
            {
                runCommand
            };

            rootCommand.Description = "Compiles, runs and analyses Yarn code.";

            // Don't provide a handler to rootCommand so that not giving a
            // subcommand results in an error

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        private static void CompileFiles(FileInfo[] inputs, bool merge, DirectoryInfo outputDirectory)
        {
            var programs = new List<Program>();
            var stringTable = new Dictionary<string,StringInfo>();

            // The list of files that failed to compile
            var failures = new List<FileInfo>();

            // The list of all files and their associated compiled results
            var results = new List<(FileInfo file, Program program, IDictionary<string,StringInfo> stringTable)>();

            foreach (var file in inputs) {
                Yarn.Program program;
                IDictionary<string,StringInfo> newStringTable;
                try {
                    Compiler.CompileFile(file.FullName, out program, out newStringTable);
                } catch (Exception e) {
                    Log.Error($"{file.FullName}: {e.Message}");
                    failures.Add(file);
                    continue;
                }

                results.Add((file, program, newStringTable));
            }

            if (merge) {
                if (failures.Count > 0) {
                    // We were asked to merge, but not all files
                    // successfully compiled. Fail at this point.
                    var errorBuilder = new StringBuilder();
                    errorBuilder.AppendLine("Not merging into a single file, because not the following files did not compile:");
                    foreach (var failure in failures) {
                        errorBuilder.AppendLine($" - {failure.Name}");
                    }
                    Log.Error(errorBuilder.ToString());
                    Environment.Exit(1);
                    return;
                }

                var mergedResult = MergeResults(results);

                WriteResult(mergedResult.Item1, mergedResult.Item2, outputDirectory, "output");                                
            } else {
                // We're writing each result separately

                foreach (var resultEntry in results) {
                    WriteResult(
                        resultEntry.program,
                        resultEntry.stringTable,
                        outputDirectory,
                        Path.GetFileNameWithoutExtension(resultEntry.file.Name)
                    );
                }
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
