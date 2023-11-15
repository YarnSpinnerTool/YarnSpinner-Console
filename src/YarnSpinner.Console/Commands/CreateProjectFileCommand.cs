namespace YarnSpinnerConsole
{
    using System.IO;
    using Yarn.Compiler;

    public static class CreateProjectFileCommand
    {
        public static void CreateProjFile(string projectName, bool unityExclusion)
        {
            Project proj = new Project();

            if (unityExclusion)
            {
                proj.ExcludeFilePatterns = new[] { "**/*~/*" };
            }

            var path = $"./{projectName}.yarnproject";

            if (File.Exists(path))
            {
                Log.Error($"Unable to create a new project file as one already exists at \"{path}\"");
                return;
            }

            proj.SaveToFile(path);
            Log.Info($"new project file created at {path}");
        }
    }
}
