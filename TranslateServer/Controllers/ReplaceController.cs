using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TranslateServer.Model.Fixes;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [AuthAdmin]
    [Route("api/[controller]")]
    [ApiController]
    public class ReplaceController : ApiController
    {
        private readonly TranslateStore _translates;
        private readonly TextsStore _texts;
        private readonly TranslateService _translateService;

        public ReplaceController(TranslateStore translates, TextsStore texts, TranslateService translateService)
        {
            _translates = translates;
            _texts = texts;
            _translateService = translateService;
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

        public class RegexRequest
        {
            public string Regex { get; set; }
            public string Replace { get; set; }
            public int Count { get; set; }
            public string[] Skip { get; set; }
        }

        // Замена заглавной буквы: '([\w,-])\sВы', '$1 вы'
        [HttpPost("{project}/regex")]
        public async Task<ActionResult> PostRegex(string project, RegexRequest request)
        {
            var reg = new Regex(request.Regex, RegexOptions.Compiled | RegexOptions.Multiline);

            var cursor = await _translates.Collection.FindAsync(t => t.Project == project && t.NextId == null && !t.Deleted);
            List<dynamic> result = new();
            var skip = request.Skip?.ToHashSet();
            foreach (var tr in cursor.ToEnumerable())
            {
                if (skip != null && skip.Contains(tr.Id)) continue;
                var replaced = reg.Replace(tr.Text, request.Replace);
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
            var tr = await _translates.Get(t => t.Id == request.Id);
            if (tr == null) return NotFound();

            await _translateService.Submit(tr.Project, tr.Volume, tr.Number, request.Replace, UserLogin, true, request.Id);

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
                var tr = await _translates.Get(t => t.Id == item.Id);
                if (tr == null) continue;

                await _translateService.Submit(tr.Project, tr.Volume, tr.Number, item.Replace, UserLogin, true, item.Id);
            }
            return Ok();
        }



        private static Dictionary<string, IReplacer> _fixers;
        private static Dictionary<string, IReplacer> GetFixers()
        {
            return _fixers ??= new()
            {
                {"twospaces", new RegexReplace("Two Spaces", @"([\.?!])( )([A-ZА-Я\d])", "$1  $3") },
                {"onespace", new RegexReplace("One Space", @"([\.?!])(  )([A-ZА-Я\d])", "$1 $3") },
                {"endemptylines", new TrimEnd("Trim End Empty Lines", '\r', '\n') },
                {"endwhitespaces", new TrimEnd("Trim End Whitespaces") },
                {"morewhitespaces", new RegexReplace(">2 Whitespaces", @"([\.?!]  )(\s+)", "$1") },
                {"dash", new RegexReplace("Dash", @" - ", " -- ") },
            };
        }

    }
}
