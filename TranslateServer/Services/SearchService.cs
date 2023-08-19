using Microsoft.Extensions.Configuration;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Documents;
using TranslateServer.Model;

namespace TranslateServer.Services
{
    public class SearchService
    {
        private readonly ElasticClient _client;
        const string SOURCE_TEXT_INDEX = "source-text";
        const string TRANSLATE_INDEX = "translate-text";

        public SearchService(IConfiguration config)
        {
            var url = config.GetConnectionString("Elastic");

            var settings = new ConnectionSettings(new Uri(url)).DisableDirectStreaming();

            _client = new ElasticClient(settings);
        }

        public Task InsertTexts(List<TextResource> texts)
        {
            return _client.IndexManyAsync(texts.Select(t =>
                new TextIndex
                {
                    Project = t.Project,
                    Volume = t.Volume,
                    Number = t.Number,
                    Text = t.Text.Replace('\n', ' '),
                }
            ), SOURCE_TEXT_INDEX);
        }

        public Task InsertTranslates(List<TextTranslate> translates)
        {
            return _client.IndexManyAsync(translates.Select(t =>
                new TextIndex
                {
                    ResourceId = t.Id,
                    Project = t.Project,
                    Volume = t.Volume,
                    Number = t.Number,
                    Text = t.Text.Replace('\n', ' '),
                }
            ), TRANSLATE_INDEX);
        }

        public async Task IndexTranslate(TextTranslate t)
        {
            var doc = new TextIndex
            {
                ResourceId = t.Id,
                Project = t.Project,
                Volume = t.Volume,
                Number = t.Number,
                Text = t.Text.Replace('\n', ' '),
            };
            await _client.DeleteByQueryAsync<TextIndex>(q => q.Query(q =>
                //q.Term(f => f.Project, t.Project) & q.Term(f => f.Volume, t.Volume) & q.Term(f => f.Number, t.Number)
                q.Term(f => f.ResourceId, t.Id)
            ).Index(TRANSLATE_INDEX));
            await _client.IndexAsync(doc, i => i.Index(TRANSLATE_INDEX));
        }

        public Task DeleteTranslate(string id)
        {
            return _client.DeleteByQueryAsync<TextIndex>(q => q.Query(q => q.Term(f => f.ResourceId, id)).Index(TRANSLATE_INDEX));
        }

        // https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/writing-queries.html

        public async Task<IEnumerable<SearchResultItem>> Search(string query, bool inSource, bool inTranslated, int? skip, int size)
        {
            query = query.Replace('\n', ' ');
            List<string> indexes = new();
            if (inSource) indexes.Add(SOURCE_TEXT_INDEX);
            if (inTranslated) indexes.Add(TRANSLATE_INDEX);
            if (indexes.Count == 0) return new List<SearchResultItem>();

            var resp = await _client.SearchAsync<TextIndex>(s => s
                .Index(indexes.ToArray())
                .IgnoreUnavailable()
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.Text).Query(query)
                    )
                )
                .Highlight(h => h
                    .PreTags("<em>")
                    .PostTags("</em>")
                    .Encoder(HighlighterEncoder.Html)
                    .HighlightQuery(q => q
                        .Match(m => m
                            .Field(f => f.Text).Query(query)
                        )
                    )
                    .Fields(fs => fs
                        .Field(f => f.Text)
                    )
                )
                .Skip(skip)
                .Size(size)
            );

            return resp.Hits.Select(h => new SearchResultItem
            {
                Id = h.Id,
                Score = h.Score,
                Project = h.Source.Project,
                Volume = h.Source.Volume,
                Number = h.Source.Number,
                Html = h.Highlight.GetValueOrDefault("text")?.FirstOrDefault() ?? h.Source.Text
            });
        }

        public async Task<IEnumerable<SearchResultItem>> SearchInProject(string project, string query, bool inSource, bool inTranslated, int? skip, int size)
        {
            if (project == null) return await Search(query, inSource, inTranslated, skip, size);

            query = query.Replace('\n', ' ');

            List<string> indexes = new();
            if (inSource) indexes.Add(SOURCE_TEXT_INDEX);
            if (inTranslated) indexes.Add(TRANSLATE_INDEX);
            if (indexes.Count == 0) return new List<SearchResultItem>();

            var resp = await _client.SearchAsync<TextIndex>(s => s
                .Index(indexes.ToArray())
                .IgnoreUnavailable()
                .Query(q => q
                    .Bool(b => b
                        .Filter(f => f.Term(f => f.Project, project))
                        .Must(m => m
                            .Match(m => m
                                .Field(f => f.Text).Query(query)
                            )
                        )
                    )
                )
                .Highlight(h => h
                    .PreTags("<em>")
                    .PostTags("</em>")
                    .Encoder(HighlighterEncoder.Html)
                    .HighlightQuery(q => q
                        .Match(m => m
                            .Field(f => f.Text).Query(query)
                        )
                    )
                    .Fields(fs => fs
                        .Field(f => f.Text)
                    )
                )
                .Skip(skip)
                .Size(size)
            );

            return resp.Hits.Select(h => new SearchResultItem
            {
                Id = h.Id,
                Score = h.Score,
                Project = h.Source.Project,
                Volume = h.Source.Volume,
                Number = h.Source.Number,
                Html = h.Highlight.GetValueOrDefault("text")?.FirstOrDefault() ?? h.Source.Text,
            });
        }

        public Task DeleteProject(string project)
        {
            return _client.DeleteByQueryAsync<TextIndex>(q => q
                .Index(new string[] { SOURCE_TEXT_INDEX, TRANSLATE_INDEX })
                .IgnoreUnavailable()
                .Query(rq => rq.Term(f => f.Project, project)));
        }

        public async Task<IEnumerable<MatchResult>> GetMatch(string project, string text)
        {
            text = text.Replace('\n', ' ');

            var resp = await _client.SearchAsync<TextIndex>(s => s
                .Index(new string[] { SOURCE_TEXT_INDEX })
                .Query(q => q
                    .Bool(b => b
                        .Filter(f => f.Term(f => f.Project, project))
                        .Must(m => m
                            .Match(m => m
                                .Field(f => f.Text).Query(text)
                                .Fuzziness(Fuzziness.Auto)
                            )
                        )
                    )
                )
                .Size(3)
            );

            return resp.Hits.Select(h => new MatchResult
            {
                Volume = h.Source.Volume,
                Number = h.Source.Number,
                Text = h.Source.Text,
                Score = h.Score.GetValueOrDefault(0)
            });
        }

        public async Task<IEnumerable<MatchResult>> GetMatch(string project, string volume, string text)
        {
            text = text.Replace('\n', ' ');
            var resp = await _client.SearchAsync<TextIndex>(s => s
                .Index(new string[] { SOURCE_TEXT_INDEX })
                .Query(q => q
                    .Bool(b => b
                        .Filter(
                            f => f.Term(f => f.Project, project),
                            f => f.MatchPhrase(f => f.Field(f => f.Volume).Query(volume))
                        )
                        .Must(m => m.Match(m => m
                            .Field(f => f.Text).Query(text)
                            .Fuzziness(Fuzziness.Auto))
                        )
                    )
                )
                .Size(3)
            );

            return resp.Hits.Select(h => new MatchResult
            {
                Volume = h.Source.Volume,
                Number = h.Source.Number,
                Text = h.Source.Text,
                Score = h.Score.GetValueOrDefault(0)
            });
        }

        public async Task<double> GetMaxScore(TextResource text)
        {
            var resp = await _client.SearchAsync<TextIndex>(s => s
                .Index(new string[] { SOURCE_TEXT_INDEX })
                .Query(q => q
                    .Bool(b => b
                        .Filter(
                            f => f.Term(f => f.Project, text.Project),
                            f => f.MatchPhrase(f => f.Field(f => f.Volume).Query(text.Volume)),
                            f => f.Term(f => f.Number, text.Number)
                        )
                        .Must(m => m
                            .Match(m => m
                                .Field(f => f.Text).Query(text.Text.Replace('\n', ' '))
                                .Fuzziness(Fuzziness.Auto)
                            )
                        )
                    )
                )
                .Size(1)
            );

            if (!resp.Hits.Any())
                return 0;
            return resp.Hits.First().Score.Value;
        }
    }
}
