namespace YarnSpinnerConsole
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Yarn;
    using Yarn.Compiler;
    using Yarn.Compiler.Upgrader;

    public class ConsoleApp
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

            // Create a root command with our two subcommands
            var rootCommand = new RootCommand
            {
                runCommand,
                compileCommand,
                upgradeCommand,
                dumpTreeCommand,
                dumpTokensCommand,
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
            try
            {
                upgradeResult = LanguageUpgrader.Upgrade(upgradeJob);
            }
            catch (ParseException e)
            {
                Log.Error($"Cannot upgrade files: parse error encountered. {e.Message}");
                return;
            }
            catch (UpgradeException e)
            {
                Log.Error($"Cannot upgrade files: upgrade error encounterd. {e.Message}");
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
            var results = new List<(FileInfo file, Program program, IDictionary<string, StringInfo> stringTable)>();

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

            var programOutputPath = Path.Combine(outputDirectory.FullName, outputName);
            var stringTableOutputPath = Path.Combine(outputDirectory.FullName, outputStringTableName);

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
        }

        private static void DumpTree(FileInfo[] input, DirectoryInfo outputDirectory, bool json)
        {
            foreach (var inputFile in input)
            {
                var source = File.ReadAllText(inputFile.FullName);
                try
                {
                    Antlr4.Runtime.Tree.IParseTree tree = Utility.GetParseTree(source);

                    string result;

                    var fileName = Path.GetFileNameWithoutExtension(inputFile.Name);
                    var outputFilePath = Path.Combine(outputDirectory.FullName, fileName + "-ParseTree");


                    if (json)
                    {
                        result = FormatParseTreeAsJSON(tree);
                        outputFilePath = Path.ChangeExtension(outputFilePath, ".json");
                    }
                    else
                    {
                        result = FormatParseTreeAsText(tree, "| ");
                        outputFilePath = Path.ChangeExtension(outputFilePath, ".txt");
                    }

                    File.WriteAllText(outputFilePath, result);

                    Log.Info($"Wrote {outputFilePath}");
                }
                catch (ParseException e)
                {
                    Log.Error($"Failed to dump {inputFile.Name} because of a parse exception: {e}");
                }
            }
        }

        private class SerializedParseNode
        {
            public int Line { get; set; }

            public int Column { get; set; }

            public string Name { get; set; }

            public string Text { get; set; }

            public string Channel { get; set; }

            public List<SerializedParseNode> Children { get; set; } = null;
        }

        private static string FormatParseTreeAsJSON(Antlr4.Runtime.Tree.IParseTree tree)
        {
            var stack = new Stack<(SerializedParseNode Parent, Antlr4.Runtime.Tree.IParseTree Node)>();

            stack.Push((null, tree));

            SerializedParseNode root = null;

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
                            if (token.Channel != Antlr4.Runtime.Lexer.DefaultTokenChannel) {
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
                try
                {
                    IEnumerable<Antlr4.Runtime.IToken> tree = Utility.GetTokens(source);

                    var nodes = tree.Select(token => new SerializedParseNode
                    {
                        Name = YarnSpinnerLexer.DefaultVocabulary.GetSymbolicName(token.Type),
                        Line = token.Line,
                        Column = token.Column,
                        Channel = token.Channel != YarnSpinnerLexer.DefaultTokenChannel ? YarnSpinnerLexer.channelNames[token.Channel] : null,
                        Text = token.Text,
                    }).ToList();

                    string result;

                    var fileName = Path.GetFileNameWithoutExtension(inputFile.Name);
                    var outputFilePath = Path.Combine(outputDirectory.FullName, fileName + "-Tokens");

                    if (json)
                    {
                        result = JsonSerializer.Serialize(nodes, JsonSerializationOptions);
                        outputFilePath = Path.ChangeExtension(outputFilePath, ".json");
                    }
                    else
                    {
                        result = string.Join("\n", nodes.Select(n => $"{n.Line}:{n.Column} {n.Name} \"{n.Text.Replace("\n", "\\n")}\""));
                        outputFilePath = Path.ChangeExtension(outputFilePath, ".txt");
                    }

                    File.WriteAllText(outputFilePath, result);
                    Log.Info($"Wrote {outputFilePath}");

                }
                catch (ParseException e)
                {
                    Log.Error($"Failed to dump {inputFile.Name} because of a parse exception: {e}");
                }
            }
        }
    }
}
