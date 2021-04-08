using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Route("api/projects/{project}/volumes/{volume}/[controller]")]
    [ApiController]
    public class TextsController : ApiController
    {
        private readonly TextsService _texts;

        public TextsController(TextsService texts)
        {
            _texts = texts;
        }

        [HttpGet]
        public async Task<ActionResult> List(string project, string volume)
        {
            var list = await _texts.Query()
                .Where(t => t.Project == project && t.Volume == volume)
                .SortAsc(t => t.Number)
                .Execute();
            return Ok(list);
        }
    }
}
