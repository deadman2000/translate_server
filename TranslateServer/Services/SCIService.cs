using Microsoft.Extensions.Options;
using SCI_Lib;

namespace TranslateServer.Services
{
    public class SCIService
    {
        private readonly string _projectsDir;

        public SCIService(IOptions<ServerConfig> config)
        {
            _projectsDir = config.Value.ProjectsDir;
        }

        public SCIPackage Load(string project)
        {
            return SCIPackage.Load($"{_projectsDir}/{project}/");
        }
    }
}
