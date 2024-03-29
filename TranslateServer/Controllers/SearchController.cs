﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public class SearchController : ApiController
    {
        private readonly SearchService _search;
        private readonly TranslateStore _translates;
        private readonly TextsStore _texts;

        public SearchController(SearchService search,
            TranslateStore translates,
            TextsStore texts,
            ProjectsStore projects
        )
        {
            _projects = projects;
            _search = search;
            _translates = translates;
            _texts = texts;
        }

        public class SearchRequest
        {
            public string Project { get; set; }
            public string Query { get; set; }
            public bool Source { get; set; } = true;
            public bool Translated { get; set; } = true;
            public bool Regex { get; set; }
            public bool IgnoreCase { get; set; }
            public int? Skip { get; set; }
            public int Size { get; set; } = 10;
        }

        [HttpPost]
        public async Task<ActionResult> Search(SearchRequest request)
        {
            if (IsSharedUser)
            {
                if (request.Project == null) return EmptySearchResult(request.Query);
                if (!await HasAccessToProject(request.Project)) return EmptySearchResult(request.Query);
            }

            IEnumerable<SearchResultItem> result;
            if (request.Regex)
                result = await RegexSearch(request);
            else
                result = await _search.SearchInProject(request.Project, request.Query, request.Source, request.Translated, request.Skip, request.Size);

            return Ok(new
            {
                request.Query,
                result
            });
        }

        private ActionResult EmptySearchResult(string query)
        {
            return Ok(new
            {
                query,
                result = Array.Empty<object>()
            });
        }

        private async Task<IEnumerable<SearchResultItem>> RegexSearch(SearchRequest request)
        {
            var opts = RegexOptions.Compiled | RegexOptions.Multiline;
            if (request.IgnoreCase)
                opts |= RegexOptions.IgnoreCase;
            var regex = new Regex(request.Query, opts);

            IEnumerable<SearchResultItem> result = new List<SearchResultItem>();

            if (request.Translated)
            {
                IAsyncCursor<TextTranslate> cursor;
                if (request.Project != null)
                    cursor = await _translates.Collection.FindAsync(t => t.Project == request.Project && t.NextId == null && !t.Deleted);
                else
                    cursor = await _translates.Collection.FindAsync(t => t.NextId == null && !t.Deleted);
                result = result.Union(cursor.ToEnumerable()
                    .Where(t => regex.IsMatch(t.Text))
                    .Select(t => new SearchResultItem
                    {
                        Project = t.Project,
                        Id = t.Id,
                        Volume = t.Volume,
                        Number = t.Number,
                        Html = Wrap(t.Text, regex),
                    }));
            }

            if (request.Source)
            {
                IAsyncCursor<TextResource> cursor;
                if (request.Project != null)
                    cursor = await _texts.Collection.FindAsync(t => t.Project == request.Project);
                else
                    cursor = await _texts.Collection.FindAsync(t => true);
                result = result.Union(cursor.ToEnumerable()
                    .Where(t => regex.IsMatch(t.Text))
                    .Select(t => new SearchResultItem
                    {
                        Project = t.Project,
                        Id = t.Id,
                        Volume = t.Volume,
                        Number = t.Number,
                        Html = Wrap(t.Text, regex),
                    }));
            }

            if (request.Skip != null)
                result = result.Skip(request.Skip.Value);

            return result.Take(request.Size);
        }

        private static string Wrap(string text, Regex regex) => regex.Replace(text, "<em>$&</em>");
    }
}
