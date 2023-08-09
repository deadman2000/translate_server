using Microsoft.Extensions.Options;
using SCI_Lib;
using System;
using System.IO;

namespace TranslateServer.Services
{
    public class SCIService
    {
        private readonly string _projectsDir;

        public SCIService(IOptions<ServerConfig> config)
        {
            _projectsDir = config.Value.ProjectsDir;
        }

        public string GetProjectPath(string project) => $"{_projectsDir}/{project}/";

        public SCIPackage Load(string project)
        {
            return SCIPackage.Load(GetProjectPath(project));
        }

        public void DeletePackage(string project)
        {
            var path = GetProjectPath(project);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        public string Copy(string project)
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            CopyDirectory(GetProjectPath(project), dir, true);
            return dir;
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }
}
