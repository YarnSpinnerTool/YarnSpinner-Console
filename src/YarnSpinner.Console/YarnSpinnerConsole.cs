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
    using Yarn.Compiler.Upgrader;
    using ClosedXML.Excel;

    /// <summary>
    /// Provides the entry point to the ysc command.
    /// </summary>
    public class YarnSpinnerConsole
    {
        private static JsonSerializerOptions JsonSerializationOptions => new JsonSerializerOptions
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

                var outputFilenameOption = new Option<string>("-n", "Output filename (default: Output.yarnc)");
                outputFilenameOption.AddAlias("--output-name");
                outputFilenameOption.Argument.SetDefaultValue("Output.yarnc");
                compileCommand.AddOption(outputFilenameOption);

                var outputStringTableOption = new Option<string>("-t", "Output string table filename (default: Output.csv");
                outputStringTableOption.AddAlias("--output-string-table-name");
                outputStringTableOption.Argument.SetDefaultValue("Output.csv");
                compileCommand.AddOption(outputStringTableOption);

                // TODO: maybe this could be derived from the value of
                // OutputName? like if you said "Test.yarnc", it'd be
                // "Test.csv", but if you left it as the default
                // "Output.yarnc", it would be "Output.csv"
            }

            compileCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo, string, string>(CompileFiles);

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

            runCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], string, bool>(RunFiles);

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

            upgradeCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], Yarn.Compiler.Upgrader.UpgradeType>(UpgradeFiles);

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

            dumpTreeCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo, bool>(DumpTree);

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

            dumpTokensCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo, bool>(DumpTokens);

            var tagCommand = new Command("tag", "Tags a Yarn script with localisation line IDs");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "The files to tag with line IDs");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                tagCommand.AddArgument(inputsArgument.ExistingOnly());

                var outputOption = new Option<DirectoryInfo>("-o", "Output directory to write the newly tagged files (default: override the input files)");
                outputOption.AddAlias("--output-directory");
                tagCommand.AddOption(outputOption.ExistingOnly());
            }

            tagCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], DirectoryInfo>(TagFiles);

            var extractCommand = new Command("extract", "Extracts strings from the provided files");
            {
                Argument<FileInfo[]> inputsArgument = new Argument<FileInfo[]>("inputs", "the files to extract strings from");
                inputsArgument.Arity = ArgumentArity.OneOrMore;
                extractCommand.AddArgument(inputsArgument.ExistingOnly());

                var exportFormat = new Option<string>("--format", "The export file format for the extracted strings, defaults to csv").FromAmong("csv", "xlsx");
                exportFormat.AddAlias("-f");
                exportFormat.Argument.SetDefaultValue("csv");
                extractCommand.AddOption(exportFormat);
            }

            extractCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create<FileInfo[], string>(ExtractStrings);

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
            };

            rootCommand.Description = "Compiles, runs and analyses Yarn code.";

            // Don't provide a handler to rootCommand so that not giving a
            // subcommand results in an error

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }

        private static void UpgradeFiles(FileInfo[] inputs, UpgradeType upgradeType)
        {
            var upgradeJob = new UpgradeJob
            {
                UpgradeType = UpgradeType.Version1to2,
                Files = inputs.Select(inputFileInfo => new CompilationJob.File
                {
                    FileName = inputFileInfo.FullName,
                    Source = File.ReadAllText(inputFileInfo.FullName),
                }).ToList(),
            };

            UpgradeResult upgradeResult;

            upgradeResult = LanguageUpgrader.Upgrade(upgradeJob);

            foreach (var diagnostic in upgradeResult.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }

            if (upgradeResult.Diagnostics.Any(d => d.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Not modifying files because errors were encountered.");
                return;
            }

            foreach (var upgradedFile in upgradeResult.Files)
            {
                if (upgradedFile.Replacements.Count() == 0)
                {
                    Log.Info($"{upgradedFile.Path}: No upgrades required.");
                }
                else
                {
                    // Write out the modified text
                    File.WriteAllText(upgradedFile.Path, upgradedFile.UpgradedSource);

                    // Log each replacement that we did
                    foreach (var replacement in upgradedFile.Replacements)
                    {
                        Log.Info($"{upgradedFile.Path}:{replacement.StartLine} \"{replacement.OriginalText}\" -> \"{replacement.ReplacementText}\"");
                    }
                }
            }
        }

        private static void RunFiles(FileInfo[] inputs, string startNode, bool autoAdvance)
        {
            // this will be a new interactive command for running yarn
            // stories will compile and then run them
            var results = CompileProgram(inputs);

            string TextForLine(string lineID)
            {
                return results.StringTable[lineID].text;
            }

            var program = results.Program;

            if (program.Nodes.ContainsKey(startNode))
            {
                var storage = new Yarn.MemoryVariableStore();
                var dialogue = new Yarn.Dialogue(storage)
                {
                    LogDebugMessage = (m) => Log.Info(m),
                    LogErrorMessage = (m) => Log.Error(m),
                };

                dialogue.SetProgram(program);
                dialogue.SetNode(startNode);

                void CommandHandler(Yarn.Command command)
                {
                    Log.Info($"Received command: {command.Text}");
                }

                void LineHandler(Yarn.Line line)
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
                }

                void OptionsHandler(Yarn.OptionSet options)
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
                    while (int.TryParse(Console.ReadLine(), out number) == false)
                    {
                        Console.WriteLine($"Select an option between 0 and {options.Options.Length - 1} to continue");
                    }

                    // rather than just trapping every possibility we
                    // just mash it into shape
                    number %= options.Options.Length;

                    if (number < 0)
                    {
                        number *= -1;
                    }

                    Log.Info($"Selecting option {number}");

                    dialogue.SetSelectedOption(number);
                }

                void NodeCompleteHandler(string completedNodeName)
                {
                    Log.Info($"Completed '{completedNodeName}' node");
                }

                void DialogueCompleteHandler()
                {
                    Log.Info("Dialogue Complete");
                }

                dialogue.LineHandler = LineHandler;
                dialogue.CommandHandler = CommandHandler;
                dialogue.OptionsHandler = OptionsHandler;
                dialogue.NodeCompleteHandler = NodeCompleteHandler;
                dialogue.DialogueCompleteHandler = DialogueCompleteHandler;

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
                Log.Error($"Unable to locate a node named {startNode} to begin. Aborting");
            }
        }

        // compiles a given yarn story designed to be called by runners or
        // the generic compile command does no writing
        private static CompilationResult CompileProgram(FileInfo[] inputs)
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

        private static void CompileFiles(FileInfo[] inputs, DirectoryInfo outputDirectory, string outputName, string outputStringTableName)
        {
            var compiledResults = CompileProgram(inputs);

            foreach (var diagnostic in compiledResults.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }

            if (compiledResults.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Not compiling files because errors were encountered.");
                return;
            }

            var programOutputPath = Path.Combine(outputDirectory.FullName, outputName);
            var stringTableOutputPath = Path.Combine(outputDirectory.FullName, outputStringTableName);
            var metadataName = Path.GetFileNameWithoutExtension(stringTableOutputPath);
            var stringMetadatOutputPath = Path.Combine(outputDirectory.FullName, $"{metadataName}-metadata.csv");

            using (var outStream = new FileStream(programOutputPath, FileMode.OpenOrCreate))
            using (var codedStream = new Google.Protobuf.CodedOutputStream(outStream))
            {
                compiledResults.Program.WriteTo(codedStream);
            }

            Log.Info($"Wrote {programOutputPath}");

            using (var writer = new StreamWriter(stringTableOutputPath))
            {
                // Use the invariant culture when writing the CSV
                var configuration = new CsvHelper.Configuration.Configuration(
                    System.Globalization.CultureInfo.InvariantCulture);

                var csv = new CsvHelper.CsvWriter(
                    writer, // write into this stream
                    configuration); // use this configuration

                var lines = compiledResults.StringTable.Select(x => new
                {
                    id = x.Key,
                    text = x.Value.text,
                    file = x.Value.fileName,
                    node = x.Value.nodeName,
                    lineNumber = x.Value.lineNumber,
                });

                csv.WriteRecords(lines);
            }

            Log.Info($"Wrote {stringTableOutputPath}");

            using (var writer = new StreamWriter(stringMetadatOutputPath))
            {
                // Use the invariant culture when writing the CSV
                var configuration = new CsvHelper.Configuration.Configuration(System.Globalization.CultureInfo.InvariantCulture);

                // not really using csvhelper correctly here but its fine for now until it all works
                var csv = new CsvHelper.CsvWriter(writer, configuration);
                csv.WriteField("id");
                csv.WriteField("node");
                csv.WriteField("lineNumber");
                csv.WriteField("tags");
                csv.NextRecord();
                foreach (var pair in compiledResults.StringTable)
                {
                    // if there are less than two items we have only the lineID itself in the metadata
                    if (pair.Value.metadata.Length < 2)
                    {
                        continue;
                    }

                    csv.WriteField(pair.Key);
                    csv.WriteField(pair.Value.nodeName);
                    csv.WriteField(pair.Value.lineNumber);
                    foreach (var record in pair.Value.metadata)
                    {
                        csv.WriteField(record);
                    }
                    csv.NextRecord();
                }

                Log.Info($"Wrote {stringMetadatOutputPath}");
            }
        }

        private static void DumpTree(FileInfo[] input, DirectoryInfo outputDirectory, bool json)
        {
            foreach (var inputFile in input)
            {
                var source = File.ReadAllText(inputFile.FullName);

                var (result, diagnostics) = Utility.ParseSource(source);

                var fileName = Path.GetFileNameWithoutExtension(inputFile.Name);
                var outputFilePath = Path.Combine(outputDirectory.FullName, fileName + "-ParseTree");

                string outputText;

                if (json)
                {
                    outputText = FormatParseTreeAsJSON(result.Tree);
                    outputFilePath = Path.ChangeExtension(outputFilePath, ".json");
                }
                else
                {
                    outputText = FormatParseTreeAsText(result.Tree, "| ");
                    outputFilePath = Path.ChangeExtension(outputFilePath, ".txt");
                }

                foreach (var diagnostic in diagnostics)
                {
                    Log.Diagnostic(diagnostic);
                }

                File.WriteAllText(outputFilePath, outputText);

                Log.Info($"Wrote {outputFilePath}");
            }
        }

        private static string FormatParseTreeAsJSON(Antlr4.Runtime.Tree.IParseTree tree)
        {
            var stack = new Stack<(SerializedParseNode Parent, Antlr4.Runtime.Tree.IParseTree Node)>();

            stack.Push((null, tree));

            SerializedParseNode root = null;

            // Walk the IParseTree, and convert it to a tree of
            // SerializedParseNodes, which we can in turn feed to the JSON
            // serializer
            while (stack.Count > 0)
            {
                var current = stack.Pop();

                var newNode = new SerializedParseNode();

                if (current.Parent == null)
                {
                    root = newNode;
                }
                else
                {
                    current.Parent.Children.Add(newNode);
                }

                switch (current.Node.Payload)
                {
                    case Antlr4.Runtime.IToken token:
                        {
                            newNode.Name = YarnSpinnerLexer.DefaultVocabulary.GetSymbolicName(token.Type);
                            newNode.Text = token.Text;
                            newNode.Line = token.Line;
                            if (token.Channel != Antlr4.Runtime.Lexer.DefaultTokenChannel)
                            {
                                newNode.Channel = YarnSpinnerLexer.channelNames[token.Channel];
                            }

                            newNode.Column = token.Column;
                            break;
                        }

                    case Antlr4.Runtime.ParserRuleContext ruleContext:
                        {
                            var start = ruleContext.Start;
                            newNode.Name = YarnSpinnerParser.ruleNames[ruleContext.RuleIndex];
                            newNode.Line = start.Line;
                            newNode.Column = start.Column;

                            newNode.Children = new List<SerializedParseNode>();

                            for (int i = ruleContext.ChildCount - 1; i >= 0; i--)
                            {
                                stack.Push((newNode, ruleContext.GetChild(i)));
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException($"Unexpected parse node type {current.Node.GetType()}");
                }
            }

            return JsonSerializer.Serialize(root, JsonSerializationOptions);
        }

        private static string FormatParseTreeAsText(Antlr4.Runtime.Tree.IParseTree tree, string indentPrefix)
        {
            var stack = new Stack<(int Indent, Antlr4.Runtime.Tree.IParseTree Node)>();

            stack.Push((0, tree));

            var sb = new StringBuilder();

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                sb.Append(string.Concat(Enumerable.Repeat(indentPrefix, current.Indent)));

                string item;

                switch (current.Node.Payload)
                {
                    case Antlr4.Runtime.IToken token:
                        {
                            // Display this token's name and text. Tokens
                            // have no children, so there's nothing else to
                            // do here.
                            var tokenName = YarnSpinnerLexer.DefaultVocabulary.GetSymbolicName(token.Type);
                            var tokenText = token.Text.Replace("\n", "\\n");
                            item = $"{token.Line}:{token.Column} {tokenName} \"{tokenText}\"";
                            break;
                        }

                    case Antlr4.Runtime.ParserRuleContext ruleContext:
                        {
                            // Display this rule's name (not its text,
                            // because that's comprised of all of the child
                            // tokens.)
                            var ruleName = YarnSpinnerParser.ruleNames[ruleContext.RuleIndex];
                            var start = ruleContext.Start;
                            item = $"{start.Line}:{start.Column} {ruleName}";

                            // Push all children into our stack; do this in
                            // reverse order of child, so that we encounter
                            // them in a reasonable order (i.e. child 0
                            // will be the next item we see)
                            for (int i = ruleContext.ChildCount - 1; i >= 0; i--)
                            {
                                var child = ruleContext.GetChild(i);
                                stack.Push((current.Indent + 1, child));
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException($"Unexpected parse node type {current.Node.GetType()}");
                }

                sb.AppendLine(item);
            }

            var result = sb.ToString();
            return result;
        }

        private static void DumpTokens(FileInfo[] input, DirectoryInfo outputDirectory, bool json)
        {
            foreach (var inputFile in input)
            {
                var source = File.ReadAllText(inputFile.FullName);

                var (result, diagnostics) = Utility.ParseSource(source);

                var nodes = result.Tokens.GetTokens().Select(token => new SerializedParseNode
                {
                    Name = YarnSpinnerLexer.DefaultVocabulary.GetSymbolicName(token.Type),
                    Line = token.Line,
                    Column = token.Column,
                    Channel = token.Channel != YarnSpinnerLexer.DefaultTokenChannel ? YarnSpinnerLexer.channelNames[token.Channel] : null,
                    Text = token.Text,
                }).ToList();

                string outputText;

                var fileName = Path.GetFileNameWithoutExtension(inputFile.Name);
                var outputFilePath = Path.Combine(outputDirectory.FullName, fileName + "-Tokens");

                if (json)
                {
                    outputText = JsonSerializer.Serialize(nodes, JsonSerializationOptions);
                    outputFilePath = Path.ChangeExtension(outputFilePath, ".json");
                }
                else
                {
                    outputText = string.Join("\n", nodes.Select(n => $"{n.Line}:{n.Column} {n.Name} \"{n.Text.Replace("\n", "\\n")}\""));
                    outputFilePath = Path.ChangeExtension(outputFilePath, ".txt");
                }

                foreach (var diagnostic in diagnostics)
                {
                    Log.Diagnostic(diagnostic);
                }

                File.WriteAllText(outputFilePath, outputText);
                Log.Info($"Wrote {outputFilePath}");
            }
        }

        // adds file tags onto the input files and writes them back out
        private static void TagFiles(FileInfo[] inputs, DirectoryInfo outputDirectory)
        {
            if (inputs == null)
            {
                Log.Fatal("No yarn files provided as inputs");
            }

            var tags = new List<string>();
            foreach (var inputFile in inputs)
            {
                var compilationJob = CompilationJob.CreateFromFiles(inputFile.FullName);
                compilationJob.CompilationType = CompilationJob.Type.StringsOnly;

                var results = Compiler.Compile(compilationJob);

                bool containsErrors = results.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);
                if (containsErrors)
                {
                    Log.Error($"Can't check for existing line tags in {inputFile.FullName} because it contains errors. Existing tags will be overwritten");
                    continue;
                }

                var existingTags = results.StringTable.Where(i => i.Value.isImplicitTag == false).Select(i => i.Key);
                tags.AddRange(existingTags);
            }

            var writeOut = new Dictionary<string, string>();
            foreach (var inputFile in inputs)
            {
                try
                {
                    var contents = File.ReadAllText(inputFile.FullName);
                    var taggedFile = Utility.AddTagsToLines(contents, tags);

                    var path = inputFile.FullName;

                    if (outputDirectory != null)
                    {
                        path = outputDirectory.FullName + inputFile.Name;
                    }
                    writeOut[path] = taggedFile;
                }
                catch
                {
                    Log.Error($"Unable to read {inputFile.FullName}");
                }
            }

            // not sure if this is better than doing it during the tagging
            // /me shrugs
            foreach (var pair in writeOut)
            {
                try
                {
                    File.WriteAllText(pair.Key, pair.Value, Encoding.UTF8);
                }
                catch
                {
                    Log.Error($"Unable to write tagged file {pair.Key}");
                }
            }
        }

        private static void ExtractStrings(FileInfo[] inputs, string format)
        {
            var compiledResults = CompileProgram(inputs);

            foreach (var diagnostic in compiledResults.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }

            if (compiledResults.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Aborting string extraction due to errors");
                return;
            }

            // contains every character we have encountered so far
            // this is needed later on for highlighting them
            // also I guess it means we can export info about characters
            HashSet<string> characters = new HashSet<string>();

            // do we have unnmaed characters?
            bool hasUnnamedCharacters = false;

            // used to hold each run of lines that are to be associated together
            List<List<(string id, string text, string character)>> lineBlocks = new List<List<(string id, string text, string character)>>();
            (string id, string text, string character) MakeLineData(string lineID)
            {
                var line = compiledResults.StringTable[lineID];

                string character = string.Empty;
                var index = line.text.IndexOf(':');
                var text = line.text;
                if (index > 0)
                {
                    character = line.text.Substring(0, index);
                    text = line.text.Substring(index + 1).TrimStart();
                    characters.Add(character);
                }
                else
                {
                    hasUnnamedCharacters = true;
                }

                return (id: lineID, text: text, character: character);
            }

            foreach (var node in compiledResults.Program.Nodes)
            {
                var blocks = Yarn.Analysis.InstructionCollectionExtensions.GetBasicBlocks(node.Value);
                var visited = new HashSet<string>();
                foreach (var block in blocks)
                {
                    RunBlock(block, blocks, visited);
                }
            }

            void RunBlock(Yarn.Analysis.BasicBlock block, IEnumerable<Yarn.Analysis.BasicBlock> blocks, HashSet<string> visited, string openingLineID = null)
            {
                if (block.PlayerVisibleContent.Count() == 0)
                {
                    // skipping this block because it has no user content within
                    return;
                }

                if (visited.Contains(block.Name))
                {
                    // we have already visited this one so we can go on without it
                    return;
                }
                visited.Add(block.Name);

                var runOfLines = new List<(string id, string text, string character)>();

                // if we are given an opening line ID we need to add that in at the top
                // this handles the case where we want options to open the set associated lines
                if (!string.IsNullOrEmpty(openingLineID))
                {
                    runOfLines.Add(MakeLineData(openingLineID));
                }

                foreach (var content in block.PlayerVisibleContent)
                {
                    // I really really dislike using objects in this manner
                    // it just feels oh so very strange to me
                    if (content is Yarn.Analysis.BasicBlock.LineElement)
                    {
                        // lines just get added to the current collection of content
                        var line = content as Yarn.Analysis.BasicBlock.LineElement;

                        runOfLines.Add(MakeLineData(line.LineID));
                    }
                    else if (content is Yarn.Analysis.BasicBlock.OptionsElement)
                    {
                        // options are special cased because of how they work
                        // an option will always be put into a block by themselves and any child content they have
                        // so this means we close off the current run of content and add it to the overall container
                        // and then make a new one for each option in the option set
                        if (runOfLines.Count() > 0)
                        {
                            lineBlocks.Add(runOfLines);
                            runOfLines = new List<(string id, string text, string character)>();
                        }

                        var options = content as Yarn.Analysis.BasicBlock.OptionsElement;
                        var jumpOptions = new Dictionary<string, Yarn.Analysis.BasicBlock>();
                        foreach (var option in options.Options)
                        {
                            var destination = blocks.First(block => block.LabelName == option.Destination);
                            if (destination != null && destination.PlayerVisibleContent.Count() > 0)
                            {
                                // there is a valid jump we need to deal with
                                // we store this and will handle it later
                                jumpOptions[option.LineID] = destination;
                            }
                            else
                            {
                                // there is no jump for this option
                                // we just add it to the collection and continue
                                runOfLines.Add(MakeLineData(option.LineID));
                                lineBlocks.Add(runOfLines);
                                runOfLines = new List<(string id, string text, string character)>();
                            }
                        }

                        // now any options without a child block have been handled we need to handle those with children
                        // in that case we want to run through each of those as if they are a new block but with the option at the top
                        foreach (var pair in jumpOptions)
                        {
                            RunBlock(pair.Value, blocks, visited, pair.Key);
                        }
                    }
                    else if (content is Yarn.Analysis.BasicBlock.CommandElement)
                    {
                        // skipping commands as they aren't lines
                        continue;
                    }
                    else
                    {
                        Log.Error("Somehow encountered a non-user facing element...");
                    }
                }

                if (runOfLines.Count() > 0)
                {
                    lineBlocks.Add(runOfLines);
                }
            }

            int lineCount = lineBlocks.Sum(l => l.Count());
            if (lineCount != compiledResults.StringTable.Count())
            {
                Log.Error($"String table has {compiledResults.StringTable.Count()} lines, we encountered {lineCount}!");
            }

            switch (format)
            {
                case "csv":
                {
                    using (var writer = new StreamWriter("./lines.csv"))
                    {
                        // Use the invariant culture when writing the CSV
                        var configuration = new CsvHelper.Configuration.Configuration(System.Globalization.CultureInfo.InvariantCulture);

                        var csv = new CsvHelper.CsvWriter(writer, configuration);
                        csv.WriteField("character");
                        csv.WriteField("text");
                        csv.WriteField("id");
                        csv.NextRecord();

                        foreach (var lines in lineBlocks)
                        {
                            foreach (var line in lines)
                            {
                                var character = line.character == string.Empty ? "NO CHAR" : line.character;
                                csv.WriteField(character);
                                csv.WriteField(line.text);
                                csv.WriteField(line.id);

                                csv.NextRecord();
                            }
                            // hack to draw a line after each block
                            csv.WriteField("---");
                            csv.WriteField("---");
                            csv.WriteField("---");
                            csv.NextRecord();
                        }
                    }

                    break;
                }
                case "xlsx":
                {
                    var wb = new XLWorkbook();
                    var sheet = wb.AddWorksheet("Amazing Dialogue!");
                    int i = 1;
                    foreach (var lines in lineBlocks)
                    {
                        foreach (var line in lines)
                        {
                            var character = line.character == string.Empty ? "NO CHAR" : line.character;
                            sheet.Cell($"A{i}").Value = character;
                            sheet.Cell($"B{i}").Value = line.text;
                            sheet.Cell($"C{i}").Value = line.id;
                            i += 1;
                        }
                        // total hack for now to draw a line after each block
                        sheet.Cell($"A{i}").Value = "---";
                        sheet.Cell($"B{i}").Value = "---";
                        sheet.Cell($"C{i}").Value = "---";
                        i += 1;
                    }

                    // adding the unnamed character into the list of characters so it will also get a colour
                    if (hasUnnamedCharacters)
                    {
                        characters.Add("NO CHAR");
                    }

                    // colouring every character
                    // we do this by moving around the hue wheel and a 20-40% saturation
                    // this creates a mostly low collision colour for labelling characters
                    int colourIncrementor = 0;
                    Random random = new Random();
                    double range = (0.4 - 0.2) + 0.2; // putting this out here so I can tweak it as needed: (max - min) + min
                    foreach (var character in characters)
                    {
                        sheet.RangeUsed().AddConditionalFormat().WhenIsTrue($"=$A1=\"{character}\"").Fill.SetBackgroundColor(ColorFromHSV(360.0 / characters.Count() * colourIncrementor, random.NextDouble() * range, 1));
                        colourIncrementor += 1;
                    }

                    XLColor ColorFromHSV(double hue, double saturation, double value)
                    {
                        int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
                        double f = (hue / 60) - Math.Floor(hue / 60);

                        value = value * 255;
                        int v = Convert.ToInt32(value);
                        int p = Convert.ToInt32(value * (1 - saturation));
                        int q = Convert.ToInt32(value * (1 - f * saturation));
                        int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

                        switch(hi)
                        {
                            case 0:
                                return XLColor.FromArgb(255, v, t, p);
                            case 1:
                                return XLColor.FromArgb(255, q, v, p);
                            case 2:
                                return XLColor.FromArgb(255, p, v, t);
                            case 3:
                                return XLColor.FromArgb(255, p, q, v);
                            case 4:
                                return XLColor.FromArgb(255, t, p, v);
                            default:
                                return XLColor.FromArgb(255, v, p, q);
                        }
                    }

                    wb.SaveAs("./lines.xlsx");
                    break;
                }
            }

            Log.Info("file written");
        }

        /// <summary>
        /// A data-only class that stores a subset of the information
        /// related to parse nodes, and designed to be serialized to JSON.
        /// A parse node is either a rule (which has child nodes), or a
        /// token.
        /// </summary>
        private class SerializedParseNode
        {
            /// <summary>
            /// Gets or sets the line number that this parse node begins on.
            /// </summary>
            /// <remarks>
            /// The first line number is 1.
            /// </remarks>
            public int Line { get; set; }

            /// <summary>
            /// Gets or sets the column number that this parse node begins on.
            /// </summary>
            public int Column { get; set; }

            /// <summary>
            /// Gets or sets the name of the rule or token that this node
            /// represents.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the text of this token. If this node
            /// represents a rule, this property will be <see
            /// langword="null"/>.
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// Gets or sets the name of the channel that this token
            /// appeared on. If this node represents a rule, this property
            /// wil be <see langword="null"/>.
            /// </summary>
            public string Channel { get; set; }

            /// <summary>
            /// Gets or sets the children of this node (that is, the rules
            /// or tokens that make up this node.)
            /// </summary>
            public List<SerializedParseNode> Children { get; set; } = null;
        }
    }
}
