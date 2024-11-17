using Microsoft.Extensions.Options;
using SCI_Lib;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Store;

namespace TranslateServer.Services
{
    public class SCIService
    {
        private readonly string _projectsDir;
        private readonly ProjectsStore _projects;

        public SCIService(IOptions<ServerConfig> config, ProjectsStore projects)
        {
            _projectsDir = config.Value.ProjectsDir;
            _projects = projects;
        }

        public string GetProjectPath(string projectCode) => $"{_projectsDir}/{projectCode}/";

        public SCIPackage Load(Project project) => Load(project.Code, project.GetEncoding());

        public async Task<SCIPackage> Load(string projectCode)
        {
            var project = await _projects.Get(p => p.Code == projectCode);
            return Load(project);
        }

        public SCIPackage Load(string project, Encoding encoding)
        {
            return SCIPackage.Load(GetProjectPath(project), encoding);
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
