using MongoDB.Driver.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Store;

namespace TranslateServer.Services
{
    public class SpellcheckCache
    {
        private readonly TranslateStore _translate;
        private readonly ConcurrentDictionary<string, int> _totals = new();

        public SpellcheckCache(TranslateStore translate)
        {
            _translate = translate;
        }

        public async Task<int> GetTotal(string project)
        {
            if (_totals.TryGetValue(project, out int count))
                return count;

            count = await _translate.Queryable()
                .Where(t => t.Project == project && !t.Deleted && t.NextId == null && t.Spellcheck != null && t.Spellcheck.Any())
                .CountAsync();

            _totals[project] = count;
            return count;
        }

        public void ResetTotal(string project)
        {
            _totals.Remove(project, out int _);
        }

        public void ResetTotal()
        {
            _totals.Clear();
        }
    }
}