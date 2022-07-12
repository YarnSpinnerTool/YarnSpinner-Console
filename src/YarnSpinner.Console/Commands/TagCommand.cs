namespace YarnSpinnerConsole
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Yarn.Compiler;

    public static class TagCommand
    {
        public static void TagFiles(FileInfo[] inputs, DirectoryInfo outputDirectory)
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
    }
}
