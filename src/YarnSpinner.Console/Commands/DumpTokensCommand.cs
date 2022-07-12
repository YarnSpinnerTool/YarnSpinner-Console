namespace YarnSpinnerConsole
{
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Yarn.Compiler;

    public static class DumpTokensCommand
    {
        public static void DumpTokens(FileInfo[] input, DirectoryInfo outputDirectory, bool json)
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
                    outputText = JsonSerializer.Serialize(nodes, YarnSpinnerConsole.JsonSerializationOptions);
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
    }
}
