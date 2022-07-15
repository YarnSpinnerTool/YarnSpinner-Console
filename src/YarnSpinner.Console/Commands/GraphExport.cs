namespace YarnSpinnerConsole
{
    using System.IO;
    using System.Linq;
    using System.Text;

    public static class GraphExport
    {
        public static void CreateGraph(FileInfo[] inputs, FileInfo output)
        {
            Log.Info("graphing!");

            var contents = new string[inputs.Length];
            for (int i = 0; i < contents.Length; i++)
            {
                contents[i] = File.ReadAllText(inputs[i].FullName);
            }

            var graph = Yarn.Compiler.Utility.DetermineNodeConnections(contents);


            if (graph.Count() > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("digraph dialogue {");
                
                foreach (var connection in graph)
                {
                    foreach (var link in connection.jumps)
                    {
                        sb.AppendLine($"\t{connection.node} -> {link};");
                    }
                }

                sb.AppendLine("}");

                string location;
                if (output == null)
                {
                    location = "./dialogue.dot";
                }
                else
                {
                    location = output.FullName;
                }
                using (StreamWriter file = new StreamWriter(location))
                {
                    file.WriteLine(sb.ToString());
                }
            }

            Log.Info("Graphed!");
        }
    }
}