namespace YarnSpinnerConsole
{
    using System.IO;
    using System.Linq;
    using Yarn.Compiler;
    using Yarn.Compiler.Upgrader;

    public static class UpgradeCommand
    {
        public static void UpgradeFiles(FileInfo[] inputs, UpgradeType upgradeType)
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

            upgradeResult = LanguageUpgrader.Upgrade(upgradeJob);

            foreach (var diagnostic in upgradeResult.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }

            if (upgradeResult.Diagnostics.Any(d => d.Severity == Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error))
            {
                Log.Error($"Not modifying files because errors were encountered.");
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

            Log.Info("Upgrade complete, compiling to determine if any errors have occurred as result of upgrade");

            // finally we do a compile of the files *just* in-case
            var compiledResults = YarnSpinnerConsole.CompileProgram(inputs);
            foreach (var diagnostic in compiledResults.Diagnostics)
            {
                Log.Diagnostic(diagnostic);
            }
        }

    }
}
