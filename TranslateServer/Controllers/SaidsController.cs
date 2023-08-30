using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCI_Lib;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using SCI_Lib.Resources.Scripts.Elements;
using SCI_Lib.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SaidsController : ApiController
    {
        private readonly SaidStore _saids;
        private readonly TextsStore _texts;
        private readonly TranslateStore _translates;
        private readonly ResCache _resCache;

        public SaidsController(SaidStore saids,
            TextsStore texts,
            TranslateStore translates,
            ResCache resCache
        )
        {
            _saids = saids;
            _texts = texts;
            _translates = translates;
            _resCache = resCache;
        }

        [HttpGet("{project}")]
        public ActionResult Scripts(string project)
        {
            var scripts = _saids.Queryable().Where(s => s.Project == project)
                .GroupBy(s => s.Script)
                .Select(gr => new
                {
                    Script = gr.Key,
                    Count = gr.Count(),
                    Approved = gr.Count(s => s.Approved)
                })
                .OrderBy(s => s.Script);
            return Ok(scripts);
        }

        [HttpGet("{project}/{script}")]
        public async Task<ActionResult> Get(string project, int script)
        {
            var saids = await _saids.Query(s => s.Project == project && s.Script == script);

            var package = await _resCache.LoadTranslated(project);

            var scr = package.GetResource<ResScript>((ushort)script).GetScript() as Script;
            var ss = scr.SaidSection;
            foreach (var s in saids)
            {
                if (string.IsNullOrEmpty(s.Patch))
                    s.Patch = ss.Saids[s.Index].Label;
                s.Validation = Validate(package, s.Patch, s.Examples);
            }

            return Ok(saids);
        }

        public class UpdateRequest
        {
            public string Project { get; set; }
            public int Script { get; set; }
            public int Index { get; set; }
            public string Patch { get; set; }
            public string[] Examples { get; set; }
        }

        [HttpPost("update")]
        public async Task<ActionResult> Update(UpdateRequest request)
        {
            await _saids.Update(s => s.Project == request.Project && s.Script == request.Script && s.Index == request.Index)
                .Set(s => s.Patch, request.Patch)
                .Set(s => s.Examples, request.Examples)
                .Execute();
            var said = await _saids.Get(s => s.Project == request.Project && s.Script == request.Script && s.Index == request.Index);
            return Ok(said);
        }

        public class ApproveRequest
        {
            public string Project { get; set; }
            public int Script { get; set; }
            public int Index { get; set; }
            public bool Approved { get; set; }
        }

        [HttpPost("approve")]
        public async Task<ActionResult> Approve(ApproveRequest request)
        {
            await _saids.Update(s => s.Project == request.Project && s.Script == request.Script && s.Index == request.Index)
                .Set(s => s.Approved, request.Approved)
                .Execute();
            return Ok();
        }

        [HttpGet("{project}/prints/{str}")]
        public async Task<ActionResult> Prints(string project, string str)
        {
            var prints = str.Split(',');

            List<dynamic> result = new();
            foreach (var pr in prints.Take(10))
            {
                var parts = pr.Split('.');
                var vol = $"text_{int.Parse(parts[0]):D3}";
                var num = int.Parse(parts[1]);
                var txt = await _texts.Get(t => t.Project == project && t.Volume == vol && t.Number == num);
                var tr = await _translates.Get(t => t.Project == project && t.Volume == vol && t.Number == num
                    && t.IsTranslate && t.NextId == null && !t.Deleted);
                result.Add(new
                {
                    txt.Text,
                    Tr = tr?.Text
                });
            }

            return Ok(result);
        }

        public class ValidateRequest
        {
            public string Said { get; set; }
            public string[] Examples { get; set; }
        }

        [HttpPost("{project}/validate")]
        public async Task<ActionResult> Validate(string project, ValidateRequest request)
        {
            var package = await _resCache.LoadTranslated(project);
            var validation = Validate(package, request.Said, request.Examples);
            return Ok(validation);
        }

        private static SaidValidation Validate(SCIPackage package, string said, string[] examples)
        {
            SaidData[] saidData;
            try
            {
                saidData = package.ParseSaid(said);
            }
            catch (SaidException sex)
            {
                return new()
                {
                    Error = sex.Message,
                    ErrWord = sex.Word,
                };
            }

            var parser = package.GetParser();
            var saidTree = parser.BuildSaidTree(saidData);
            if (saidTree == null)
            {
                return new()
                {
                    Error = "Can't build said tree"
                };
            }

            List<SaidParsing> examplesValidations = new();
            if (examples != null)
                foreach (var text in examples)
                {
                    SaidParsing parsing = new();
                    examplesValidations.Add(parsing);

                    var result = parser.Tokenize(text);
                    parsing.Words = result.Words.Select(w => new WordValidation()
                    {
                        Word = w.Word,
                        IsValid = w.IsValid,
                        Ids = w.Ids?.Select(i => $"{(ushort)i.Class:x3}:{i.Group:x3}")
                    });

                    if (!result.IsValid)
                    {
                        parsing.Error = "Can't parse word";
                        parsing.ErrWords = result.Words.Where(w => !w.IsValid).Select(w => w.Word);
                    }
                    else
                    {
                        var parseTree = parser.ParseGNF(result.Words);
                        if (parseTree == null)
                        {
                            parsing.Error = "Can't build parse tree";
                        }
                        else
                        {
                            parsing.Match = parser.Match(parseTree, saidTree);
                            parsing.Tree = parseTree.GetTree("parse-tree");
                        }
                    }
                }

            return new()
            {
                Said = saidData.Select(s => s.Hex),
                SaidTree = saidTree.GetTree("said-tree"),
                Examples = examplesValidations,
                Valid = examplesValidations.All(v => v.Match)
            };
        }
    }
}
