using Microsoft.AspNetCore.Mvc;
using SCI_Lib.Resources;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExtApproveController : ApiController
    {
        private readonly ResCache _resCache;
        private readonly TextsStore _textsStore;
        private readonly TranslateService _translateService;

        public ExtApproveController(ResCache resCache, TextsStore textsStore, TranslateService translateService)
        {
            _resCache = resCache;
            _textsStore = textsStore;
            _translateService = translateService;
        }

        public class ApproveRequest
        {
            public string Project { get; set; }
            public string Type { get; set; }
            public ushort Res { get; set; }
            public int? Noun { get; set; }
            public int? Verb { get; set; }
            public int? Seq { get; set; }
            public int? Cond { get; set; }
            public int? Index { get; set; }


            public string Volume { get; set; }

            /// <summary>
            /// Source text
            /// </summary>
            public string Text { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult> Approve(ApproveRequest request)
        {
            if (request.Type == "source")
            {
                await _textsStore.Update()
                                .Where(t => t.Project == request.Project && t.Volume == request.Volume && t.Text == request.Text && !t.TranslateApproved)
                                .Set(t => t.TranslateApproved, true)
                                .Execute();

                await _translateService.UpdateVolumeProgress(request.Project, request.Volume);
                await _translateService.UpdateProjectProgress(request.Project);

                return Ok();
            }


            string volume;
            int index;
            var package = await _resCache.Load(request.Project);

            if (request.Type == "msg")
            {
                var msg = package.GetResource<ResMessage>(request.Res);
                index = msg.GetMessages().FindIndex(
                    m => m.Noun == request.Noun.Value &&
                    m.Verb == request.Verb.Value &&
                    m.Seq == request.Seq.Value &&
                    m.Cond == request.Cond.Value);

                if (index == -1)
                    return NotFound();

                volume = Volume.FileNameToCode(msg.FileName);
            }
            else
            {
                index = request.Index.Value;
                ResType t;
                if (request.Type == "scr")
                    t = ResType.Script;
                else if (request.Type == "txt")
                    t = ResType.Text;
                else
                    return BadRequest();
                volume = Volume.FileNameToCode(package.GetResFileName(t, request.Res));
            }

            await _textsStore.Update()
                            .Where(t => t.Project == request.Project && t.Volume == volume && t.Number == index && !t.TranslateApproved)
                            .Set(t => t.TranslateApproved, true)
                            .Execute();

            await _translateService.UpdateVolumeProgress(request.Project, volume);
            await _translateService.UpdateProjectProgress(request.Project);

            return Ok();
        }
    }
}
