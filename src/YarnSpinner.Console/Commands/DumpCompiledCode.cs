namespace YarnSpinnerConsole
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Yarn.Compiler;

    public static class DumpCompiledCodeCommand
    {
        public static void DumpCompiledCode(FileInfo[] input, bool allowPreviewFeatures) {
            var compiledResults = YarnSpinnerConsole.CompileProgram(input, allowPreviewFeatures);

            System.Func<string, string> stringLookupHelper = (input) =>
            {
                if (compiledResults.StringTable.TryGetValue(input, out var result))
                {
                    return result.text;
                }
                else
                {
                    return null;
                }
            };

            Console.WriteLine(Yarn.Compiler.Utility.GetCompiledCodeAsString(compiledResults.Program, null, compiledResults));
        }
    }
}
