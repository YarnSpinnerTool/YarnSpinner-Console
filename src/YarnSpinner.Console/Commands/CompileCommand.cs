namespace YarnSpinnerConsole
{
    using System.IO;
    using System.Linq;
    using Yarn.Compiler;

    public static class CompileCommand
    {
        public static void CompileFiles(FileInfo[] inputs, DirectoryInfo outputDirectory, string outputName, string outputStringTableName, string outputMetadataTableName)
        {
            var compiledResults = YarnSpinnerConsole.CompileProgram(inputs);

            foreach (var diagnostic in compiledResults.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }

            if (compiledResults.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Not compiling files because errors were encountered.");
                return;
            }

            // ok so basically in here we do a quick check of the number of files we have
            // if we only have one AND output is the default then we use that as our output name instead of Output
            if (inputs.Length == 1 && outputName.Equals("Output"))
            {
                outputName = inputs[0].Name.Remove(inputs[0].Extension.Length + 1);
            }

            if (string.IsNullOrEmpty(outputStringTableName))
            {
                outputStringTableName = $"{outputName}-Lines.csv";
            }
            if (string.IsNullOrEmpty(outputMetadataTableName))
            {
                outputMetadataTableName = $"{outputName}-Metadata.csv";
            }

            var programOutputPath = Path.Combine(outputDirectory.FullName, $"{outputName}.yarnc");
            var stringTableOutputPath = Path.Combine(outputDirectory.FullName, outputStringTableName);
            var stringMetadatOutputPath = Path.Combine(outputDirectory.FullName, outputMetadataTableName);

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
                    // filter out line: metadata
                    var metadata = pair.Value.metadata.Where(metadata => !metadata.StartsWith("line:")).ToList();
                    if (metadata.Count == 0)
                    {
                        continue;
                    }

                    csv.WriteField(pair.Key);
                    csv.WriteField(pair.Value.nodeName);
                    csv.WriteField(pair.Value.lineNumber);
                    foreach (var record in metadata)
                    {
                        csv.WriteField(record);
                    }

                    csv.NextRecord();
                }

                Log.Info($"Wrote {stringMetadatOutputPath}");
            }
        }
    }
}
