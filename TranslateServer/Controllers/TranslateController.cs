using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model;
using TranslateServer.Requests;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TranslateController : ApiController
    {
        private readonly TranslateService _translate;
        private readonly TextsService _texts;
        private readonly VolumesService _volumes;
        private readonly ProjectsService _projects;
        private readonly SearchService _search;

        public TranslateController(TranslateService translate, TextsService texts, VolumesService volumes, ProjectsService projects, SearchService search)
        {
            _translate = translate;
            _texts = texts;
            _volumes = volumes;
            _projects = projects;
            _search = search;
        }

        public class SubmitRequest
        {
            public string Project { get; set; }

            public string Volume { get; set; }

            public int Number { get; set; }

            public string TranslateId { get; set; }

            public string Text { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult> Submit([FromBody] SubmitRequest request)
        {
            var txt = await _texts.Get(t => t.Project == request.Project && t.Volume == request.Volume && t.Number == request.Number);
            if (txt == null)
                return NotFound();

            TextTranslate translate = new()
            {
                Project = request.Project,
                Volume = request.Volume,
                Number = request.Number,
                Text = request.Text,
                Author = UserLogin,
                DateCreate = DateTime.UtcNow,
            };

            await _translate.Insert(translate);

            await _translate.Update(t => t.Id == request.TranslateId
                                      && t.NextId == null
                                      && !t.Deleted)
                .Set(t => t.NextId, translate.Id)
                .Execute();

            bool needUpdate = false;
            if (!txt.HasTranslate)
            {
                await _texts.Update(t => t.Id == txt.Id).Set(t => t.HasTranslate, true).Execute();
                needUpdate = true;
            }

            if (txt.TranslateApproved)
            {
                await _texts.Update(t => t.Id == txt.Id).Set(t => t.TranslateApproved, false).Execute();
                needUpdate = true;
            }

            if (needUpdate)
                await UpdateVolumeProgress(request.Project, request.Volume);

            await _volumes.Update(v => v.Project == request.Project && v.Code == request.Volume).Set(v => v.LastSubmit, DateTime.UtcNow).Execute();
            await _projects.Update(p => p.Code == request.Project).Set(p => p.LastSubmit, DateTime.UtcNow).Execute();

            if (request.TranslateId != null)
                await _search.DeleteTranslate(request.TranslateId);
            await _search.IndexTranslate(translate);

            return Ok(new TranslateInfo(translate));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            var tr = await _translate.GetById(id);
            if (tr == null)
                return NotFound();

            if (!IsAdmin && tr.Author != UserLogin)
                return Forbid();

            await _translate.Update(t => t.Id == id)
                .Set(t => t.Deleted, true)
                .Execute();

            TextTranslate newTr = null;

            if (!IsAdmin) // Удаляем все предыдущие переводы до первого чужого
            {
                TextTranslate next = tr;
                while (true)
                {
                    var prev = await _translate.Get(t => t.NextId == next.Id && !t.Deleted);
                    if (prev == null) break;

                    if (prev.Author == UserLogin)
                    {
                        await _translate.Update(t => t.Id == prev.Id)
                            .Set(t => t.Deleted, true)
                            .Execute();
                        next = prev;
                    }
                    else
                    {
                        await _translate.Update(t => t.Id == prev.Id)
                            .Set(t => t.NextId, null)
                            .Execute();
                        prev.NextId = null;
                        newTr = prev;
                        break;
                    }
                }
            }

            var another = await _translate.Query(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number && !t.Deleted && t.NextId == null);
            if (!another.Any())
            {
                await _texts.Update(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number)
                    .Set(t => t.HasTranslate, false)
                    .Set(t => t.TranslateApproved, false)
                    .Execute();

                await UpdateVolumeProgress(tr.Project, tr.Volume);
            }

            await _search.DeleteTranslate(id);

            return Ok(newTr != null ? new TranslateInfo(newTr) : null);
        }

        private async Task UpdateVolumeProgress(string project, string volume)
        {
            var translated = await _texts.Collection.Aggregate()
                .Match(t => t.Project == project && t.Volume == volume && t.HasTranslate)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(t => t.Letters),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            var approved = await _texts.Collection.Aggregate()
                .Match(t => t.Project == project && t.Volume == volume && t.TranslateApproved)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(t => t.Letters),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            await _volumes.Update(v => v.Project == project && v.Code == volume)
                .Set(v => v.TranslatedLetters, translated != null ? translated.Letters : 0)
                .Set(v => v.TranslatedTexts, translated != null ? translated.Count : 0)
                .Set(v => v.ApprovedLetters, approved != null ? approved.Letters : 0)
                .Set(v => v.ApprovedTexts, approved != null ? approved.Count : 0)
                .Execute();

            await UpdateProjectProgress(project);
        }

        private async Task UpdateProjectProgress(string project)
        {
            var res = await _volumes.Collection.Aggregate()
                .Match(t => t.Project == project)
                .Group(t => true,
                g => new
                {
                    Letters = g.Sum(v => v.TranslatedLetters),
                    Count = g.Sum(v => v.TranslatedTexts),
                    ALetters = g.Sum(v => v.ApprovedLetters),
                    ACount = g.Sum(v => v.ApprovedTexts),
                })
                .FirstOrDefaultAsync();

            await _projects.Update(p => p.Code == project)
                .Set(p => p.TranslatedLetters, res.Letters)
                .Set(p => p.TranslatedTexts, res.Count)
                .Set(p => p.ApprovedLetters, res.ALetters)
                .Set(p => p.ApprovedTexts, res.ACount)
                .Execute();
        }

        [HttpGet("{translateId}/history")]
        public async Task<ActionResult> History(string translateId)
        {
            var translate = await _translate.GetById(translateId);
            if (translate == null) return NotFound();

            var all = await _translate.Query(t => t.Project == translate.Project && t.Volume == translate.Volume && t.Number == translate.Number && t.NextId != null);

            var dict = all.ToDictionary(t => t.NextId, t => t);

            List<TranslateInfo> result = new();
            result.Add(new TranslateInfo(translate));
            while (true)
            {
                if (!dict.TryGetValue(translate.Id, out var prev)) break;
                result.Add(new TranslateInfo(prev));
                translate = prev;
            }

            return Ok(result);
        }

        public class ApproveRequest
        {
            public bool Approved { get; set; }
        }

        [AuthAdmin]
        [HttpPost("{translateId}/approve")]
        public async Task<ActionResult> Approve(string translateId, [FromBody] ApproveRequest request)
        {
            var tr = await _translate.GetById(translateId);
            if (tr == null) return NotFound();

            await _texts.Update()
                .Where(t => t.Project == tr.Project && t.Volume == tr.Volume && t.Number == tr.Number)
                .Set(t => t.TranslateApproved, request.Approved)
                .Execute();

            await UpdateVolumeProgress(tr.Project, tr.Volume);

            return Ok();
        }
    }
}
