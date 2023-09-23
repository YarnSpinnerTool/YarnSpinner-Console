namespace YarnSpinnerConsole
{
    using System;
    using System.IO;
    using System.Linq;
    using Yarn;

    public static class RunCommand
    {
        public static void RunFiles(FileInfo[] inputs, string startNode, bool autoAdvance)
        {
            // this will be a new interactive command for running yarn
            // stories will compile and then run them
            var results = YarnSpinnerConsole.CompileProgram(inputs);

            if (results.Diagnostics.Any(d => d.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Not running files because errors were encountered.");
                foreach (var diagnostic in results.Diagnostics)
                {
                    Log.Diagnostic(diagnostic);
                }
                return;
            }

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

                dialogue.Library.RegisterFunction("visited", (string nodeName) =>
                {
                    var visitedCountVariableName = Library.GenerateUniqueVisitedVariableForNode(nodeName);

                    return storage.TryGetValue<int>(visitedCountVariableName, out var count)
                        ? count > 0
                        : false;
                });

                dialogue.Library.RegisterFunction("visited_count", (string nodeName) =>
                {
                    var visitedCountVariableName = Library.GenerateUniqueVisitedVariableForNode(nodeName);

                    return storage.TryGetValue<int>(visitedCountVariableName, out var count)
                        ? count
                        : 0;
                });

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
    }
}
