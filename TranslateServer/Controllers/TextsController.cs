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

        public TextsController(TextsService texts, TranslateService translate)
        {
            _texts = texts;
            _translate = translate;
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

            return Ok(list.Select(t => new
            {
                Source = t,
                Translates = tdict.TryGetValue(t.Number, out var tr) ? tr : null,
            }));
        }
    }
}
