using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCI_Lib.Resources.Vocab;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class WordsController : ApiController
    {
        private readonly WordsStore _words;
        private readonly ResCache _resCache;

        public WordsController(WordsStore words, ResCache resCache)
        {
            _words = words;
            _resCache = resCache;
        }

        [HttpGet("{project}")]
        public async Task<ActionResult> Get(string project)
        {
            var words = await _words.Query(w => w.Project == project);
            return Ok(words.GroupBy(w => w.WordId)
                .Select(gr => new
                {
                    Id = gr.Key,
                    Group = Word.GetGroup(gr.Key),
                    Class = Word.GetClass(gr.Key),
                    Usage = gr.Where(w => !w.IsTranslate).Select(w => w.Usage).FirstOrDefault(),
                    Words = gr.Where(w => !w.IsTranslate).Select(w => w.Text).FirstOrDefault(),
                    Translate = gr.Where(w => w.IsTranslate).Select(w => w.Text).FirstOrDefault()
                })
                .OrderBy(w => w.Group)
                .ThenBy(w => w.Class));
        }

        [HttpGet("{project}/{id}")]
        public async Task<ActionResult> Get(string project, int id)
        {
            var package = await _resCache.LoadTranslated(project);
            return Ok(package.GetWords().Where(w => w.Group == id).Select(w => w.Text));
        }

        public class PostRequest
        {
            public string Words { get; set; }
            public int Cl { get; set; }
            public int? Gr { get; set; }
        }

        [HttpPost("{project}")]
        public async Task<ActionResult> Post(string project, PostRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Words))
            {
                if (!request.Gr.HasValue) return Ok();

                var id = Word.GetId((ushort)request.Gr.Value, (ushort)request.Cl);
                await _words.Delete(w => w.Project == project && w.IsTranslate && w.WordId == id);
                _resCache.Clear(project);
                return Ok();
            }
            else
            {
                var package = await _resCache.LoadTranslated(project);
                var wordToIds = package.GetWordIds();

                var words = request.Words.Split(',').Select(w => w.Trim()).Where(w => w.Length > 0);

                foreach (var w in words)
                {
                    if (wordToIds.TryGetValue(w, out var ids))
                    {
                        if (!request.Gr.HasValue || !ids.Contains((ushort)request.Gr.Value))
                        {
                            return BadRequest(new { Message = $"Word '{w}' already exists" });
                        }
                    }
                }

                if (!request.Gr.HasValue)
                    request.Gr = await NextGroupId(project);

                var id = Word.GetId((ushort)request.Gr.Value, (ushort)request.Cl);
                await _words.Update(w => w.Project == project && w.IsTranslate && w.WordId == id)
                     .Set(w => w.Text, string.Join(", ", words))
                     .Upsert();

                var word = await GetWord(project, id);
                _resCache.Clear(project);
                return Ok(word);
            }
        }

        private async Task<dynamic> GetWord(string project, int id)
        {
            var words = await _words.Query(w => w.Project == project && w.WordId == id);
            return new
            {
                Id = id,
                Group = Word.GetGroup(id),
                Class = Word.GetClass(id),
                Usage = words.Where(w => !w.IsTranslate).Select(w => w.Usage).FirstOrDefault(),
                Words = words.Where(w => !w.IsTranslate).Select(w => w.Text).FirstOrDefault(),
                Translate = words.Where(w => w.IsTranslate).Select(w => w.Text).FirstOrDefault()
            };
        }

        private async Task<ushort> NextGroupId(string project)
        {
            var words = await _words.Query(w => w.Project == project);
            var ids = words.Select(w => Word.GetGroup(w.WordId)).Distinct().OrderBy(v => v).ToArray();
            for (int i = 0; i < ids.Length - 1; i++)
            {
                if (ids[i] != ids[i + 1] - 1)
                    return (ushort)(ids[i] + 1);
            }
            return 0;
        }

        [HttpGet("dublicate/{project}")]
        public async Task<ActionResult> GetDublicate(string project)
        {
            var package = await _resCache.LoadTranslated(project);
            var words = package.GetWords()
                .GroupBy(w => w.Text)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            return Ok(words);
        }
    }
}
