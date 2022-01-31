using Microsoft.Extensions.Configuration;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

            var settings = new ConnectionSettings(new Uri(url));

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
                    Link = $"/projects/{t.Project}/{t.Volume}#t{t.Number}",
                    Text = t.Text,
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
                    Link = $"/projects/{t.Project}/{t.Volume}#t{t.Number}",
                    Text = t.Text,
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
                Link = $"/projects/{t.Project}/{t.Volume}#t{t.Number}",
                Text = t.Text,
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

        public async Task<IEnumerable<SearchResultItem>> Search(string query)
        {
            var resp = await _client.SearchAsync<TextIndex>(s => s
                .Index(new string[] { SOURCE_TEXT_INDEX, TRANSLATE_INDEX })
                .IgnoreUnavailable()
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.Text).Query(query)
                    //.Fuzziness(Fuzziness.Auto)
                    )
                )
                /*.Query(q=>q
                    .Fuzzy(z=>z
                        .Field(f=>f.Text).Value(query)
                    )
                )*/
                .Highlight(h => h
                    .PreTags("<em>")
                    .PostTags("</em>")
                    .Encoder(HighlighterEncoder.Html)
                    .HighlightQuery(q => q
                        .Match(m => m
                            .Field(f => f.Text).Query(query)
                        //.Fuzziness(Fuzziness.Auto)
                        )
                    )
                    .Fields(fs => fs
                        .Field(f => f.Text)
                    )
                )
                .Size(10)
            );

            return resp.Hits.Select(h => new SearchResultItem
            {
                Project = h.Source.Project,
                Volume = h.Source.Volume,
                Html = h.Highlight.GetValueOrDefault("text")?.FirstOrDefault() ?? h.Source.Text,
                Link = h.Source.Link
            });
        }

        public Task DeleteProject(string project)
        {
            return _client.DeleteByQueryAsync<TextIndex>(q => q
                .Index(new string[] { SOURCE_TEXT_INDEX, TRANSLATE_INDEX })
                .IgnoreUnavailable()
                .Query(rq => rq.Term(f => f.Project, project)));
        }
    }
}
