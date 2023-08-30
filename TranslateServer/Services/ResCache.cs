using Microsoft.Extensions.DependencyInjection;
using SCI_Lib;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TranslateServer.Store;

namespace TranslateServer.Services
{
    public class ResCache
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, SCIPackage> _sciCache = new();

        public ResCache(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<SCIPackage> LoadTranslated(string project)
        {
            using var scope = _serviceProvider.CreateScope();
            var sci = scope.ServiceProvider.GetRequiredService<SCIService>();
            var words = scope.ServiceProvider.GetRequiredService<WordsStore>();
            var suffixes = scope.ServiceProvider.GetRequiredService<SuffixesStore>();

            if (_sciCache.TryGetValue(project, out var package)) return package;

            package = sci.Load(project);
            await words.Apply(package, project);
            await suffixes.Apply(package, project);

            _sciCache[project] = package;

            return package;
        }

        public void Clear(string project)
        {
            _sciCache.TryRemove(project, out var _);
        }
    }
}
