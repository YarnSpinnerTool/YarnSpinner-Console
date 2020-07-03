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

        public static void Fatal(string text)
        {
            PrintLine("🚨 ERROR", ConsoleColor.Red, text);
            Environment.Exit(1);
        }
    }
}
