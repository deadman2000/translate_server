using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Services;
using TranslateServer.Store;

namespace TranslateServer.Mcp
{
    /// <summary>
    /// Core business logic for the AI/MCP agent.
    /// This service is used by both the REST MCP controller and the real MCP protocol server.
    /// </summary>
    public class McpAgentService
    {
        private readonly McpOptions _options;
        private readonly ProjectsStore _projects;
        private readonly VolumesStore _volumes;
        private readonly TextsStore _texts;
        private readonly TranslateStore _translates;
        private readonly CommentsStore _comments;
        private readonly SearchService _search;
        private readonly TranslateService _translateService;

        public McpAgentService(
            IOptions<McpOptions> options,
            ProjectsStore projects,
            VolumesStore volumes,
            TextsStore texts,
            TranslateStore translates,
            CommentsStore comments,
            SearchService search,
            TranslateService translateService)
        {
            _options = options.Value;
            _projects = projects;
            _volumes = volumes;
            _texts = texts;
            _translates = translates;
            _comments = comments;
            _search = search;
            _translateService = translateService;
        }

        public string AgentName => _options.AgentName ?? "AI";

        // ============================================
        // DISCOVERY
        // ============================================

        public async Task<object> GetStatusAsync()
        {
            return new
            {
                enabled = _options.Enabled,
                agentName = AgentName,
                serverTime = DateTime.UtcNow
            };
        }

        public async Task<IEnumerable<object>> GetProjectsAsync()
        {
            var list = await _projects.All();
            return list.Select(p => new
            {
                code = p.Code,
                name = p.Name,
                engine = p.Engine,
                status = p.Status.ToString(),
                texts = p.Texts,
                letters = p.Letters,
                translatedTexts = p.TranslatedTexts,
                translatedLetters = p.TranslatedLetters,
                approvedTexts = p.ApprovedTexts,
                approvedLetters = p.ApprovedLetters,
                lastSubmit = p.LastSubmit
            });
        }

        public async Task<IEnumerable<object>> GetVolumesAsync(string project)
        {
            var volumes = await _volumes.Query(v => v.Project == project);
            return volumes
                .OrderBy(v => v.Code)
                .Select(v => new
                {
                    code = v.Code,
                    name = v.Name,
                    texts = v.Texts,
                    letters = v.Letters,
                    translatedTexts = v.TranslatedTexts,
                    translatedLetters = v.TranslatedLetters,
                    approvedTexts = v.ApprovedTexts,
                    approvedLetters = v.ApprovedLetters,
                    lastSubmit = v.LastSubmit
                });
        }

        // ============================================
        // TEXTS + CONTEXT
        // ============================================

        public async Task<object> GetTextsAsync(string project, string volume, string status, int limit, int offset)
        {
            var query = _texts.Query()
                .Where(t => t.Project == project && t.Volume == volume);

            var allTexts = await query.SortAsc(t => t.Number).Execute();

            IEnumerable<TextResource> filtered = status?.ToLower() switch
            {
                "untranslated" => allTexts.Where(t => !t.HasTranslate),
                "unapproved" => allTexts.Where(t => t.HasTranslate && !t.TranslateApproved),
                "approved" => allTexts.Where(t => t.TranslateApproved),
                _ => allTexts
            };

            var page = filtered.Skip(offset).Take(Math.Min(limit, 500)).ToList();

            var numbers = page.Select(t => t.Number).ToList();
            var translates = await _translates.Query(t =>
                t.Project == project &&
                t.Volume == volume &&
                numbers.Contains(t.Number) &&
                t.NextId == null &&
                !t.Deleted);

            var trDict = translates.ToDictionary(t => t.Number);

            return new
            {
                total = filtered.Count(),
                returned = page.Count,
                items = page.Select(t => new
                {
                    number = t.Number,
                    source = t.Text,
                    hasTranslate = t.HasTranslate,
                    translateApproved = t.TranslateApproved,
                    letters = t.Letters,
                    currentTranslation = trDict.TryGetValue(t.Number, out var tr)
                        ? new { id = tr.Id, author = tr.Author, text = tr.Text, date = tr.DateCreate }
                        : null
                })
            };
        }

        /// <summary>
        /// The most important method for LLM agents.
        /// Returns rich context around a specific text line.
        /// </summary>
        public async Task<object> GetTextContextAsync(string project, string volume, int number, int contextLines)
        {
            var source = await _texts.Get(t => t.Project == project && t.Volume == volume && t.Number == number);
            if (source == null)
                return null;

            int from = Math.Max(0, number - contextLines);
            int to = number + contextLines;

            var contextTexts = await _texts.Query(t =>
                t.Project == project &&
                t.Volume == volume &&
                t.Number >= from &&
                t.Number <= to);

            var before = contextTexts
                .Where(t => t.Number < number)
                .OrderBy(t => t.Number)
                .Select(t => new { number = t.Number, text = t.Text })
                .ToList();

            var after = contextTexts
                .Where(t => t.Number > number)
                .OrderBy(t => t.Number)
                .Select(t => new { number = t.Number, text = t.Text })
                .ToList();

            var current = await _translates.Get(t =>
                t.Project == project &&
                t.Volume == volume &&
                t.Number == number &&
                t.NextId == null &&
                !t.Deleted);

            object currentTranslation = null;
            List<object> comments = null;

            if (current != null)
            {
                currentTranslation = new
                {
                    id = current.Id,
                    author = current.Author,
                    editor = current.Editor,
                    text = current.Text,
                    dateCreate = current.DateCreate,
                    isAiGenerated = current.Author == AgentName || current.Editor == AgentName,
                    spellcheck = current.Spellcheck
                };

                var cmts = await _comments.GetComments(current);
                comments = cmts.Select(c => new
                {
                    id = c.Id,
                    author = c.Author,
                    text = c.Text,
                    date = c.DateCreate
                }).Cast<object>().ToList();
            }

            var historyCount = await _translates.Collection.CountDocumentsAsync(t =>
                t.Project == project &&
                t.Volume == volume &&
                t.Number == number &&
                !t.Deleted);

            return new
            {
                project,
                volume,
                number,
                source = new { text = source.Text, letters = source.Letters },
                before,
                after,
                currentTranslation,
                comments,
                historyCount = (int)historyCount,
                translateApproved = source.TranslateApproved
            };
        }

        // ============================================
        // SEARCH
        // ============================================

        public async Task<object> SearchAsync(string project, string query, bool source, bool translated, int size)
        {
            var results = await _search.SearchInProject(
                project,
                query,
                source,
                translated,
                skip: 0,
                size: Math.Min(size, 100));

            return new
            {
                query,
                project,
                results = results.Select(r => new
                {
                    project = r.Project,
                    volume = r.Volume,
                    number = r.Number,
                    text = r.Text ?? r.Html,
                    score = r.Score
                })
            };
        }

        // ============================================
        // WRITING (AI proposals)
        // ============================================

        public async Task<object> ProposeTranslationAsync(string project, string volume, int number, string text, string reason, double? confidence)
        {
            var translate = await _translateService.Submit(
                project,
                volume,
                number,
                text,
                author: AgentName,
                approveTransfer: false,
                prevTranslateId: null);

            if (translate == null)
                return null;

            if (!string.IsNullOrWhiteSpace(reason))
            {
                var comment = new Comment
                {
                    TranslateId = translate.FirstId ?? translate.Id,
                    Project = translate.Project,
                    Volume = translate.Volume,
                    Number = translate.Number,
                    Author = AgentName,
                    Text = $"[AI Proposal] {reason}" + (confidence.HasValue ? $" (confidence: {confidence:P0})" : ""),
                    DateCreate = DateTime.UtcNow
                };
                await _comments.Insert(comment);
            }

            return new
            {
                id = translate.Id,
                author = translate.Author,
                text = translate.Text,
                message = "Proposal created successfully"
            };
        }

        public async Task<Comment> AddCommentAsync(string translateId, string text)
        {
            var tr = await _translates.GetById(translateId);
            if (tr == null)
                return null;

            var comment = new Comment
            {
                TranslateId = tr.FirstId ?? tr.Id,
                Project = tr.Project,
                Volume = tr.Volume,
                Number = tr.Number,
                Author = AgentName,
                Text = text,
                DateCreate = DateTime.UtcNow
            };

            await _comments.Insert(comment);
            return comment;
        }

        public async Task<object> GetHistoryAsync(string translateId)
        {
            var translate = await _translates.GetById(translateId);
            if (translate == null)
                return null;

            var all = await _translates.Query(t =>
                t.Project == translate.Project &&
                t.Volume == translate.Volume &&
                t.Number == translate.Number &&
                t.NextId != null);

            var dict = all.ToDictionary(t => t.NextId, t => t);

            var result = new List<object>();
            var current = translate;

            while (current != null)
            {
                result.Add(new
                {
                    id = current.Id,
                    author = current.Author,
                    text = current.Text,
                    dateCreate = current.DateCreate
                });

                dict.TryGetValue(current.Id, out current);
            }

            return new
            {
                project = translate.Project,
                volume = translate.Volume,
                number = translate.Number,
                history = result
            };
        }

        // Helper used by the old controller
        public async Task<bool> ProjectExistsAsync(string project)
        {
            if (string.IsNullOrWhiteSpace(project)) return false;
            var p = await _projects.GetProject(project);
            return p != null;
        }
    }
}
