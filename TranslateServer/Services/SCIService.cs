using Microsoft.Extensions.Options;
using SCI_Lib;
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
    }
}
