using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model.Fixes;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [AuthAdmin]
    [Route("api/[controller]")]
    [ApiController]
    public class FixesController : ControllerBase
    {
        private readonly TranslateStore _translates;
        private readonly TextsStore _texts;

        public FixesController(TranslateStore translates, TextsStore texts)
        {
            _translates = translates;
            _texts = texts;
        }

        [HttpGet("modes")]
        public ActionResult Modes()
        {
            return Ok(GetFixers().Select(kv => new
            {
                mode = kv.Key,
                desc = kv.Value.Description
            }));
        }

        public class FixesRequest
        {
            public string Mode { get; set; }
            public int Count { get; set; }
            public string[] Skip { get; set; }
        }

        [HttpPost("{project}/get")]
        public async Task<ActionResult> Get(string project, FixesRequest request)
        {
            if (!GetFixers().TryGetValue(request.Mode, out var replacer)) return NotFound();

            var cursor = await _translates.Collection.FindAsync(t => t.Project == project && t.NextId == null && !t.Deleted);
            List<dynamic> result = new();
            var skip = request.Skip?.ToHashSet();
            foreach (var tr in cursor.ToEnumerable())
            {
                if (skip != null && skip.Contains(tr.Id)) continue;
                var replaced = replacer.Replace(tr.Text);
                if (replaced != tr.Text)
                {
                    var src = await _texts.Get(t => t.Project == project && t.Volume == tr.Volume && t.Number == tr.Number);

                    result.Add(new
                    {
                        tr.Id,
                        tr.Volume,
                        tr.Number,
                        src = src.Text,
                        tr.Text,
                        replaced
                    });
                    if (result.Count >= request.Count) break;
                }
            }
            return Ok(result);
        }

        public class ApplyRequest
        {
            public string Id { get; set; }
            public string Replace { get; set; }
        }

        [HttpPost("apply")]
        public async Task<ActionResult> Apply(ApplyRequest request)
        {
            await _translates.Update(t => t.Id == request.Id)
                .Set(t => t.Text, request.Replace)
                .Execute();
            return Ok();
        }

        public class ApplyManyRequest
        {
            public ApplyRequest[] Items { get; set; }
        }

        [HttpPost("applyMany")]
        public async Task<ActionResult> ApplyMany(ApplyManyRequest request)
        {
            foreach (var item in request.Items)
            {
                await _translates.Update(t => t.Id == item.Id)
                    .Set(t => t.Text, item.Replace)
                    .Execute();
            }
            return Ok();
        }



        private static Dictionary<string, IReplacer> _fixers;
        private static Dictionary<string, IReplacer> GetFixers()
        {
            return _fixers ??= new()
            {
                {"twospaces", new RegexReplace("Two Spaces", @"([\.?!])( )([A-ZА-Я\d])", "$1  $3") },
                {"endemptylines", new TrimEnd("Trim End Empty Lines", '\r', '\n') },
                {"endwhitespaces", new TrimEnd("Trime End Whitespaces") },
                {"morewhitespaces", new RegexReplace(">2 Whitespaces", @"([\.?!]  )(\s+)", "$1") },
                {"dash", new RegexReplace("Dash", @"—", "--") },
                {"dash2", new RegexReplace("Dash2", @" - ", " -- ") },
                {"camelcase", new RegexReplace("Camel Case fix", @"([,\w]\s)(О)тдел", "$1отдел") }
            };
        }

    }
}
