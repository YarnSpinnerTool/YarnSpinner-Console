namespace YarnSpinnerConsole
{
    using System;
    using System.IO;
    using System.Linq;
    using Yarn.Compiler;

    public static class ListSourcesCommand
    {
        public static void ListSources(FileInfo yarnproject)
        {
            var project = Project.LoadFromFile(yarnproject.FullName);
            foreach (var file in project.SourceFiles)
            {
                Console.WriteLine(Path.GetRelativePath(yarnproject.Directory.FullName, file));
            }
        }
    }
}