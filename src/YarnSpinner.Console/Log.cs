namespace YarnSpinnerConsole
{
    using System;

    static class Log
    {
        public static void PrintLine(string prefix, ConsoleColor color, string text)
        {
            Console.ResetColor();
            Console.ForegroundColor = color;
            Console.Write(prefix);
            Console.Write(": ");
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void Info(string text)
        {
            PrintLine("💁‍♂️ INFO", ConsoleColor.Blue, text);
        }

        public static void Warn(string text)
        {
            PrintLine("⚠️ WARNING", ConsoleColor.Yellow, text);
        }

        public static void Error(string text)
        {
            PrintLine("🚨 ERROR", ConsoleColor.Red, text);
        }

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        public static void Fatal(string text)
        {
            PrintLine("🚨 ERROR", ConsoleColor.Red, text);
            Environment.Exit(1);
        }

        public static void Diagnostic(Yarn.Compiler.Diagnostic diagnostic)
        {
            var messagePrefix = string.IsNullOrEmpty(diagnostic.FileName) ? string.Empty : $"{diagnostic.FileName}: {diagnostic.Line}:{diagnostic.Column} ";

            var message = messagePrefix + diagnostic.Message;

            switch (diagnostic.Severity)
            {
                case Yarn.Compiler.Diagnostic.DiagnosticSeverity.Error:
                    Error(message);
                    break;
                case Yarn.Compiler.Diagnostic.DiagnosticSeverity.Warning:
                    Warn(message);
                    break;
                case Yarn.Compiler.Diagnostic.DiagnosticSeverity.Info:
                    Info(message);
                    break;
            }
        }
    }
}
