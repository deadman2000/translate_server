using Microsoft.AspNetCore.Mvc;
using SCI_Lib.Resources;
using System.Threading.Tasks;
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
            public int? Index { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult> Approve(ApproveRequest request)
        {
            string volume;
            int index;

            if (request.Type == "msg")
            {
                var _package = _resCache.Load(request.Project);

                var msg = _package.GetResource<ResMessage>(request.Res);
                index = msg.GetMessages().FindIndex(m => m.Noun == request.Noun.Value && m.Verb == request.Verb.Value);

                if (index == -1)
                    return NotFound();

                volume = $"{request.Res}_msg";
            }
            else
            {
                index = request.Index.Value;
                if (request.Type == "scr")
                    volume = $"{request.Res}_scr";
                else if (request.Type == "txt")
                    volume = $"{request.Res}_tex";
                else
                    return BadRequest();
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
