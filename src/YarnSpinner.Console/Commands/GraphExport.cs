namespace YarnSpinnerConsole
{
    using System.IO;
    using System.Linq;
    using System.Text;

    public static class GraphExport
    {
        public static void CreateGraph(FileInfo[] inputs, FileInfo output, bool clustering)
        {
            var contents = new string[inputs.Length];
            for (int i = 0; i < contents.Length; i++)
            {
                contents[i] = File.ReadAllText(inputs[i].FullName);
            }

            var graph = Yarn.Compiler.Utility.DetermineNodeConnections(contents);

            if (graph.Count() > 0)
            {
                // using three individual builders is a bit lazy but it means I can turn stuff on and off as needed
                StringBuilder sb = new StringBuilder();
                StringBuilder links = new StringBuilder();
                StringBuilder sub = new StringBuilder();
                sb.AppendLine("digraph dialogue {");

                if (clustering)
                {
                    int i = 0;
                    foreach (var cluster in graph)
                    {
                        if (cluster.Count == 0)
                        {
                            continue;
                        }
                        
                        // they need to be named clusterSomething to be clustered
                        sub.AppendLine($"\tsubgraph cluster{i}{{");
                        sub.Append("\t\t");
                        foreach (var node in cluster)
                        {
                            sub.Append($"{node.node} ");
                        }
                        sub.AppendLine(";");
                        sub.AppendLine("\t}");
                        i++;
                    }
                }

                foreach (var cluster in graph)
                {
                    foreach (var connection in cluster)
                    {
                        if (connection.hasPositionalInformation)
                        {
                            sb.AppendLine($"\t{connection.node} [");
                            sb.AppendLine($"\t\tpos = \"{connection.position.x},{connection.position.y}\"");
                            sb.AppendLine("\t]");
                        }

                        foreach (var link in connection.jumps)
                        {
                            links.AppendLine($"\t{connection.node} -> {link};");
                        }
                    }
                }   

                sb.Append(links);
                sb.Append(sub);

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
                    file.Write(sb.ToString());
                }
            }

            Log.Info("Dialogue graph created");
        }
    }
}