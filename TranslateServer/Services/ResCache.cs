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
        private readonly ConcurrentDictionary<string, SCIPackage> _sourceCache = new();
        private readonly ConcurrentDictionary<string, SCIPackage> _translatedCache = new();

        public ResCache(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<SCIPackage> Load(string project)
        {
            if (_sourceCache.TryGetValue(project, out var package)) return package;

            using var scope = _serviceProvider.CreateScope();
            var sci = scope.ServiceProvider.GetRequiredService<SCIService>();

            package = await sci.Load(project);

            _sourceCache[project] = package;

            return package;
        }

        public async Task<SCIPackage> LoadTranslated(string project)
        {
            if (_translatedCache.TryGetValue(project, out var package)) return package;

            using var scope = _serviceProvider.CreateScope();
            var sci = scope.ServiceProvider.GetRequiredService<SCIService>();
            var words = scope.ServiceProvider.GetRequiredService<WordsStore>();
            var suffixes = scope.ServiceProvider.GetRequiredService<SuffixesStore>();

            package = await sci.Load(project);
            await words.Apply(package, project);
            await suffixes.Apply(package, project);

            _translatedCache[project] = package;

            return package;
        }

        public void ClearTranslated(string project)
        {
            _translatedCache.TryRemove(project, out var _);
        }
    }
}
