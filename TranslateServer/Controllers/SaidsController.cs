using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCI_Lib;
using SCI_Lib.Resources;
using SCI_Lib.Resources.Scripts;
using SCI_Lib.Resources.Scripts.Elements;
using SCI_Lib.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TranslateServer.Documents;
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
        public async Task<ActionResult> Scripts(string project)
        {
            var saids = await _saids.Query(s => s.Project == project);

            await CheckValidation(saids);

            var scripts = saids
                .GroupBy(s => s.Script)
                .Select(gr => new
                {
                    Script = gr.Key,
                    Count = gr.Count(),
                    Approved = gr.Count(s => s.Approved),
                    Valid = gr.All(s => s.IsValid.GetValueOrDefault(true))
                })
                .OrderBy(s => s.Script);
            return Ok(scripts);
        }

        private async Task CheckValidation(List<SaidDocument> saids)
        {
            if (!saids.Any()) return;

            var package = await _resCache.LoadTranslated(saids.First().Project);

            foreach (var said in saids.Where(t => t.IsValid == null))
            {
                said.IsValid = Validate(package, said.Patch, said.Tests).Valid;
                await _saids.Update(s => s.Id == said.Id)
                    .Set(s => s.IsValid, said.IsValid)
                    .Execute();
            }
        }

        [HttpGet("{project}/{script}")]
        public async Task<ActionResult> Get(string project, int script)
        {
            var saids = await _saids.Query(s => s.Project == project && s.Script == script);

            var package = await _resCache.LoadTranslated(project);

            var scr = package.GetResource<ResScript>((ushort)script).GetScript() as Script;
            var ss = scr.SaidSection;
            foreach (var said in saids)
            {
                if (string.IsNullOrEmpty(said.Patch))
                    said.Patch = ss.Saids[said.Index].Label;
                said.Validation = Validate(package, said.Patch, said.Tests);
                if (said.Validation.Valid != said.IsValid)
                {
                    await _saids.Update(s => s.Id == said.Id)
                        .Set(s => s.IsValid, said.Validation.Valid)
                        .Execute();
                }
            }

            return Ok(saids);
        }

        public class UpdateRequest
        {
            public string Project { get; set; }
            public int Script { get; set; }
            public int Index { get; set; }
            public string Patch { get; set; }
            public SaidTest[] Tests { get; set; }
        }

        [HttpPost("update")]
        public async Task<ActionResult> Update(UpdateRequest request)
        {
            var package = await _resCache.LoadTranslated(request.Project);

            var validation = Validate(package, request.Patch, request.Tests);

            await _saids.Update(s => s.Project == request.Project && s.Script == request.Script && s.Index == request.Index)
                .Set(s => s.Patch, request.Patch)
                .Set(s => s.Tests, request.Tests)
                .Set(s => s.IsValid, validation.Valid)
                .Execute();

            var said = await _saids.Get(s => s.Project == request.Project && s.Script == request.Script && s.Index == request.Index);
            said.Validation = validation;
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
            public SaidTest[] Tests { get; set; }
        }

        [HttpPost("{project}/validate")]
        public async Task<ActionResult<SaidValidation>> Validate(string project, ValidateRequest request)
        {
            var package = await _resCache.LoadTranslated(project);
            var validation = Validate(package, request.Said, request.Tests);
            return Ok(validation);
        }

        private static SaidValidation Validate(SCIPackage package, string said, SaidTest[] tests)
        {
            if (said == null)
            {
                return new()
                {
                    Valid = true
                };
            }

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

            if (tests == null || tests.Length == 0) return new()
            {
                Said = saidData.Select(s => s.Hex),
                SaidTree = saidTree.GetTree("said-tree"),
                Tests = new List<SaidParsing>(0),
                Valid = true
            };

            List<SaidParsing> examplesValidations = new();
            foreach (var test in tests)
            {
                SaidParsing parsing = new();
                examplesValidations.Add(parsing);

                var result = parser.Tokenize(test.Said);
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
                        parsing.Success = parsing.Match == test.Positive;
                        parsing.Tree = parseTree.GetTree("parse-tree");
                    }
                }
            }

            return new()
            {
                Said = saidData.Select(s => s.Hex),
                SaidTree = saidTree.GetTree("said-tree"),
                Tests = examplesValidations,
                Valid = examplesValidations.All(v => v.Success)
            };
        }

        public class TranslateRequest
        {
            public string Said { get; set; }
        }

        [HttpPost("{project}/translate")]
        public async Task<ActionResult<string>> Translate(string project, TranslateRequest request)
        {
            var package = await _resCache.LoadTranslated(project);
            var saidData = package.ParseSaid(request.Said);
            var idToWord = package.GetIdToWord();

            StringBuilder sb = new();

            foreach (var said in saidData)
            {
                if (said.IsOperator)
                    sb.Append(said.Letter);
                else if (said.OriginalWord.Any(l => l >= 'a' && l <= 'z'))
                    sb.Append(idToWord[said.Data]);
                else
                    sb.Append(said.OriginalWord);
            }

            return Ok(sb.ToString());
        }
    }
}
