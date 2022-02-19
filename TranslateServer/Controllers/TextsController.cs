using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Requests;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/projects/{project}/volumes/{volume}/[controller]")]
    [ApiController]
    public class TextsController : ApiController
    {
        private readonly TextsService _texts;
        private readonly TranslateService _translate;
        private readonly VideoReferenceService _references;

        public TextsController(TextsService texts, TranslateService translate, VideoReferenceService references)
        {
            _texts = texts;
            _translate = translate;
            _references = references;
        }

        [HttpGet]
        public async Task<ActionResult> List(string project, string volume)
        {
            // Texts
            var list = await _texts.Query()
                .Where(t => t.Project == project && t.Volume == volume)
                .SortAsc(t => t.Number)
                .Execute();

            // Translates
            var trList = await _translate.Query()
                .Where(t => t.Project == project && t.Volume == volume && t.NextId == null && !t.Deleted)
                .SortAsc(t => t.Number)
                .Execute();
            var tdict = trList.GroupBy(t => t.Number).ToDictionary(t => t.Key, t => t.Select(tr => new TranslateInfo(tr)).ToArray());

            // References
            var refs = await _references.Query(r => r.Project == project && r.Volume == volume);
            var rdict = refs.GroupBy(r => r.Number).ToDictionary(r => r.Key, g => g.Select(r => new
            {
                r.VideoId,
                r.Frame,
                r.Score,
            }));

            return Ok(list.Select(t => new
            {
                Source = t,
                Translates = tdict.TryGetValue(t.Number, out var tr) ? tr : null,
                Refs = rdict.TryGetValue(t.Number, out var r) ? r : null
            }));
        }
    }
}
