namespace YarnSpinnerConsole
{
    using System.IO;

    public static class BrowseCompiledBinaryCommand
    {
        public static void BrowseBinary(FileInfo compiledInput)
        {
            var data = File.ReadAllBytes(compiledInput.FullName);
            var program = Yarn.Program.Parser.ParseFrom(data);

            if (program == null)
            {
                Log.Fatal($"Unable to read {compiledInput.Name} as a yarn program");
                return;
            }

            // nodes
            Log.PrintLine($"{compiledInput.Name} contains {program.Nodes.Count} nodes:");
            foreach (var node in program.Nodes)
            {
                Log.PrintLine($"- {node.Key}");

                foreach (var header in node.Value.Headers)
                {
                    if (header.Key == "title")
                    {
                        continue;
                    }
                    
                    Log.PrintLine($"\t- {header.Key}: \"{header.Value}\"");
                }
            }

            // declared vars
            Log.PrintLine($"\n{compiledInput.Name} contains {program.InitialValues.Count} declarations:");
            foreach (var declaration in program.InitialValues)
            {
                Log.PrintLine($"- {declaration.Key}");
                var value = declaration.Value;

                switch (value.ValueCase)
                {
                    case Yarn.Operand.ValueOneofCase.StringValue:
                        Log.PrintLine("\ttype: String");
                        Log.PrintLine($"\tinitial value: \"{value.StringValue}\"");
                        break;
                    case Yarn.Operand.ValueOneofCase.BoolValue:
                        Log.PrintLine("\ttype: Boolean");
                        Log.PrintLine($"\tinitial value: {value.BoolValue}");
                        break;
                    case Yarn.Operand.ValueOneofCase.FloatValue:
                        Log.PrintLine("\ttype: Number");
                        Log.PrintLine($"\tinitial value: {value.FloatValue}");
                        break;
                    default:
                        Log.PrintLine($"unknown variable {value.ToString()}");
                        break;
                }
            }
        }
    }
}
