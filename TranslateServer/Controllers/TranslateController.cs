using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TranslateController : ApiController
    {
        private readonly TranslateService _translate;

        public TranslateController(TranslateService translate)
        {
            _translate = translate;
        }

        public class SubmitRequest
        {
            public string Project { get; set; }

            public string Volume { get; set; }

            public int Number { get; set; }

            public string Text { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult> Submit([FromBody] SubmitRequest request)
        {
            var login = User.Identity.Name;

            TextTranslate translate = new()
            {
                Project = request.Project,
                Volume = request.Volume,
                Number = request.Number,
                Text = request.Text,
                Author = login,
                DateCreate = DateTime.UtcNow,
            };

            await _translate.Insert(translate);

            await _translate.Update(t => t.Project == request.Project
                                      && t.Volume == request.Volume
                                      && t.Number == request.Number
                                      && t.Author == login
                                      && t.Id != translate.Id
                                      && t.NextId == null
                                      && !t.Deleted)
                .Set(t => t.NextId, translate.Id)
                .Execute();

            return Ok();
        }
    }
}
