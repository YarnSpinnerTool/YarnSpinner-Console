#nullable enable

namespace YarnSpinnerConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Yarn;

    public static class RunCommand
    {
        private static async ValueTask<string?> ReadLineAsync()
        {
            using var inputReader = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding);
            return await inputReader.ReadLineAsync();

        }
        private class RunCommandResponder : IDialogueResponder
        {
            public RunCommandResponder(bool autoAdvance, System.Func<string, string> textForLine)
            {
                this.AutoAdvance = autoAdvance;
                this.TextForLine = textForLine;
            }

            public bool AutoAdvance { get; }
            public Func<string, string> TextForLine { get; }

            public bool CanCallFunction(string functionName)
            {
                // This runner doesn't implement any functions
                return false;
            }

            public ValueTask HandleCommand(Command command, CancellationToken token)
            {
                Log.Info($"Received command: {command.Text}");
                return ValueTask.CompletedTask;
            }

            public ValueTask HandleDialogueComplete()
            {
                Log.Info("Dialogue Complete");
                return ValueTask.CompletedTask;
            }

            public async ValueTask HandleLine(Line line, CancellationToken token)
            {
                if (AutoAdvance)
                {
                    Console.WriteLine(TextForLine(line.ID));
                }
                else
                {
                    Console.Write(TextForLine(line.ID));
                    await ReadLineAsync();
                }

            }

            public ValueTask HandleNodeComplete(string node, CancellationToken token)
            {
                Log.Info($"Completed '{node}' node");
                return ValueTask.CompletedTask;
            }

            public ValueTask HandleNodeStart(string node, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public async ValueTask<int> HandleOptions(OptionSet options, CancellationToken token)
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
                while (int.TryParse(await ReadLineAsync(), out number) == false)
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

                return number;
            }

            public ValueTask<IConvertible> InvokeFunction(string functionName, IConvertible[] parameters, CancellationToken token)
            {
                throw new NotImplementedException("This responder does not implement any functions");
            }

            public ValueTask PrepareForLines(List<string> lineIDs, CancellationToken token)
            {
                // No-op
                return ValueTask.CompletedTask;
            }
        }

        public static async Task RunFiles(FileInfo[] inputs, string startNode, bool autoAdvance, bool allowPreviewFeatures, CancellationToken cancellationToken)
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

            string? TextForLine(string lineID)
            {
                return results.StringTable?[lineID].text;
            }

            var program = results.Program;

            if (program == null || !program.Nodes.ContainsKey(startNode))
            {
                Log.Error($"Unable to locate a node named {startNode} to begin. Aborting");
                return;
            }


            var storage = new Yarn.MemoryVariableStore();
            var dialogue = new Yarn.Dialogue(storage)
            {
                LogDebugMessage = (m) => Log.Info(m),
                LogErrorMessage = (m) => Log.Error(m),
                Program = program
            };

            await dialogue.SetNode(startNode);

            try
            {
                await dialogue.Start();
            }
            catch (InvalidOperationException ex)
            {
                Log.Fatal($"Undefined function encountered: {ex.Message}");
            }

        }
    }
}
