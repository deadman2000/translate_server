using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SynonymsController : ApiController
    {
        private readonly SynonymStore _synonyms;
        private readonly ResCache _resCache;

        public SynonymsController(SynonymStore synonyms, ResCache resCache)
        {
            _synonyms = synonyms;
            _resCache = resCache;
        }

        [HttpGet("{project}/{script}")]
        public async Task<ActionResult> Get(string project, int script)
        {
            var package = await _resCache.LoadTranslated(project);
            var idToWord = package.GetIdToWord();

            var list = await _synonyms.Query(s => s.Project == project && s.Script == script);
            return Ok(list.OrderByDescending(s => s.Index)
                .ThenBy(s => s.Id)
                .Select(s => new
                {
                    s.Id,
                    s.Index,
                    s.WordA,
                    s.WordB,
                    s.Delete,
                    WordAStr = idToWord[s.WordA],
                    WordBStr = idToWord[s.WordB],
                })
            );
        }

        public class CreateRequest
        {
            public string WordA { get; set; }
            public string WordB { get; set; }
        }

        [HttpPost("{project}/{script}")]
        public async Task<ActionResult> Create(string project, int script, CreateRequest request)
        {
            var package = await _resCache.LoadTranslated(project);

            var idA = package.GetWordId(request.WordA);
            var idB = package.GetWordId(request.WordB);

            List<string> errors = new();
            if (idA == null)
                errors.Add($"Word '{request.WordA}' not found");
            else if (idA.Length > 1)
                errors.Add($"Word '{request.WordA}' multiple id's");

            if (idB == null)
                errors.Add($"Word '{request.WordB}' not found");
            else if (idB.Length > 1)
                errors.Add($"Word '{request.WordB}' multiple id's");

            if (errors.Count > 0)
                return BadRequest(new
                {
                    Message = string.Join("; ", errors)
                });

            var doc = new SynonymDocument()
            {
                Project = project,
                Script = script,
                WordA = idA[0],
                WordB = idB[0]
            };
            await _synonyms.Insert(doc);

            var idToWord = package.GetIdToWord();

            return Ok(new
            {
                doc.Id,
                doc.Index,
                doc.WordA,
                doc.WordB,
                doc.Delete,
                WordAStr = idToWord[doc.WordA],
                WordBStr = idToWord[doc.WordB],
            });
        }

        [HttpPost("restore/{id}")]
        public async Task<ActionResult> Restore(string id)
        {
            await _synonyms.Update(s => s.Id == id)
                .Set(s => s.Delete, false)
                .Execute();
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var syn = await _synonyms.GetById(id);
            if (syn == null) return Ok();

            if (syn.Index.HasValue)
            {
                await _synonyms.Update(s => s.Id == id)
                    .Set(s => s.Delete, true)
                    .Execute();
            }
            else
            {
                await _synonyms.DeleteOne(s => s.Id == id);
            }
            return Ok();
        }
    }
}
