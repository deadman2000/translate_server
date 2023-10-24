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
                .Where(t => t.Project == project && !t.Deleted && t.NextId == null && t.Spellcheck != null && t.Spellcheck.Any())
                .OrderBy(t => t.Volume).ThenBy(t => t.Number)
                .Take(10)
                .Select(t => new
                {
                    t.Id,
                    t.Volume,
                    t.Number,
                    t.Text,
                    t.Spellcheck
                });
            return Ok(translates);
        }

        public class SkipRequest
        {
            public string Id { get; set; }
            public string Word { get; set; }
        }

        [HttpPost("skip")]
        public async Task<ActionResult> Skip(SkipRequest request)
        {
            if (!string.IsNullOrEmpty(request.Word))
            {
                var tr = await _translate.GetById(request.Id);
                var spellcheck = tr.Spellcheck.Where(s => s.Word != request.Word).ToArray();

                await _translate.Update(t => t.Id == request.Id)
                    .Set(t => t.Spellcheck, spellcheck)
                    .Execute();
            }
            else
            {
                await _translate.Update(t => t.Id == request.Id)
                    .Set(t => t.Spellcheck, Array.Empty<SpellResult>())
                    .Execute();
            }
            return Ok();
        }

        [HttpGet("{project}/total")]
        public ActionResult GetTotal(string project)
        {
            var count = _translate.Queryable()
                .Where(t => t.Project == project && !t.Deleted && t.NextId == null && t.Spellcheck != null && t.Spellcheck.Any())
                .Count();
            return Ok(new { total = count});
        }

    }
}
