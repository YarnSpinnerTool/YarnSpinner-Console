namespace YarnSpinnerConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using Yarn.Compiler;

    public static class DumpTreeCommand
    {
        public static void DumpTree(FileInfo[] input, DirectoryInfo outputDirectory, bool json)
        {
            input = CompileCommand.GetYarnFiles(input);
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

            return JsonSerializer.Serialize(root, YarnSpinnerConsole.JsonSerializationOptions);
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
    }
}
