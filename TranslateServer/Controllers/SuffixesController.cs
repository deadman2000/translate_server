﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCI_Lib;
using SCI_Lib.Resources.Vocab;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SuffixesController : ApiController
    {
        private readonly SuffixesStore _suffixes;
        private readonly ResCache _resCache;

        public SuffixesController(SuffixesStore suffixes, ResCache resCache)
        {
            _suffixes = suffixes;
            _resCache = resCache;
        }

        [HttpGet("{project}")]
        public async Task<ActionResult> Get(string project)
        {
            var package = await _resCache.LoadTranslated(project);

            var list = await _suffixes.Query(s => s.Project == project);
            return Ok(list.OrderByDescending(s => s.IsTranslate)
                .ThenBy(s => s.InClass)
                .ThenBy(s => s.Input)
                .ThenBy(s => s.Output)
                .Select(s => new
                {
                    s.Id,
                    s.IsTranslate,
                    s.Input,
                    s.InClass,
                    s.Output,
                    s.OutClass,
                    Words = GetWordsCount(package, s)
                })
            );
        }

        private static int GetWordsCount(SCIPackage package, SuffixDocument s)
        {
            var suffix = new Suffix(s.Output, (ushort)s.OutClass, s.Input, (ushort)s.InClass);
            return package.GetWords().Count(suffix.IsMatch);
        }

        public class CreateRequest
        {
            public int InCl { get; set; }
            public string Inp { get; set; }
            public int OutCl { get; set; }
            public string Out { get; set; }

            public void Prepare()
            {
                Inp = Inp.TrimStart('*');
                Out = Out.TrimStart('*');
            }
        }

        [HttpPost("{project}")]
        public async Task<ActionResult> Create(string project, CreateRequest request)
        {
            request.Prepare();
            if (request.Inp.Length == 0 && request.Out.Length == 0 || request.InCl == 0 || request.OutCl == 0)
                return BadRequest(new { Message = "Invalid suffix" });

            var doc = new SuffixDocument
            {
                Project = project,
                InClass = request.InCl,
                Input = request.Inp,
                OutClass = request.OutCl,
                Output = request.Out,
                IsTranslate = true,
            };
            await _suffixes.Insert(doc);
            _resCache.ClearTranslated(project);
            return Ok(doc);
        }


        [HttpPost("{project}/{id}")]
        public async Task<ActionResult> Update(string project, string id, CreateRequest request)
        {
            request.Prepare();
            if (request.Inp.Length == 0 && request.Out.Length == 0 || request.InCl == 0 || request.OutCl == 0)
                return BadRequest(new { Message = "Invalid suffix" });

            await _suffixes.Update(s => s.Id == id && s.Project == project)
                .Set(s => s.InClass, request.InCl)
                .Set(s => s.Input, request.Inp)
                .Set(s => s.OutClass, request.OutCl)
                .Set(s => s.Output, request.Out)
                .Execute();
            _resCache.ClearTranslated(project);
            var suff = await _suffixes.GetById(id);
            return Ok(suff);
        }


        [HttpDelete("{project}/{id}")]
        public async Task<ActionResult> Delete(string project, string id)
        {
            _resCache.ClearTranslated(project);
            await _suffixes.DeleteOne(s => s.Id == id && s.Project == project);
            return Ok();
        }


        public class TestRequest
        {
            public string Word { get; set; }
        }

        [HttpPost("{project}/test")]
        public async Task<ActionResult> Test(string project, TestRequest request)
        {
            var package = await _resCache.LoadTranslated(project);

            var suffixes = package.GetSuffixes();
            var txtToWord = package.GetTxtWords();

            if (!txtToWord.TryGetValue(request.Word.ToLower(), out var words))
                return BadRequest(new { Message = $"Unknown word '{request.Word}'" });

            if (words.Length > 1)
                return BadRequest(new { Message = $"Word '{request.Word}' multiple id's" });

            var word = words[0];
            List<string> result = new();
            foreach (var s in suffixes)
            {
                if (s.IsMatch(word, out var w))
                    result.Add($"{w}  {s.SuffixClass}");
            }

            return Ok(result.Order());
        }

        [HttpPost("{project}/copy/{src}")]
        public async Task<ActionResult> Copy(string project, string src)
        {
            var sourceSuff = await _suffixes.Query(s => s.Project == src && s.IsTranslate);

            if (!sourceSuff.Any())
                return Ok();

            await _suffixes.Delete(s => s.Project == project && s.IsTranslate);
            sourceSuff.ForEach(s =>
            {
                s.Id = null;
                s.Project = project;
            });
            await _suffixes.Insert(sourceSuff);

            return Ok();
        }
    }
}
