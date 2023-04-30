using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model.Yandex;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SpellcheckController : ControllerBase
    {
        private readonly TranslateStore _translate;

        public SpellcheckController(TranslateStore translate)
        {
            _translate = translate;
        }

        [HttpGet("{project}")]
        public ActionResult GetErrors(string project)
        {
            var translates = _translate.Queryable()
                .Where(t => t.Project == project && !t.Deleted && t.NextId == null && t.Spellcheck != null && t.Spellcheck.Length > 0)
                .OrderBy(t => t.Volume).ThenBy(t => t.Number)
                .Take(10)
                .Select(t => new
                {
                    t.Volume,
                    t.Number,
                    t.Text,
                    t.Spellcheck
                });
            return Ok(translates);
        }


        public class SkipRequest
        {
            public string Volume { get; set; }
            public int Number { get; set; }
        }

        [HttpPost("{project}/skip")]
        public async Task<ActionResult> Skip(string project, SkipRequest request)
        {
            await _translate.Update(t => t.Project == project && t.Volume == request.Volume && t.Number == request.Number && !t.Deleted && t.NextId == null)
                .Set(t => t.Spellcheck, Array.Empty<SpellResult>())
                .Execute();
            return Ok();
        }



        [HttpGet("{project}/total")]
        public ActionResult GetTotal(string project)
        {
            var count = _translate.Queryable()
                .Where(t => t.Project == project && !t.Deleted && t.NextId == null && t.Spellcheck != null && t.Spellcheck.Length > 0)
                .Count();
            return Ok(new { total = count});
        }

    }
}
