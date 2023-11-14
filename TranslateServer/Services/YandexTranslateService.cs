using Flurl.Http;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TranslateServer.Services
{
    public class YandexTranslateService
    {
        const string URL = "https://translate.api.cloud.yandex.net/translate/v2/translate";

        private string YandexKey { get; }

        private readonly object Glossary = new
        {
            glossaryData = new
            {
                glossaryPairs = new object[]
                {
                    GlossaryPair("Oups", "Упс"),
                    GlossaryPair("Asgard", "Асгард"),
                    GlossaryPair("Ignatius", "Игнатий"),
                }
            }
        };

        private static object GlossaryPair(string src, string tr) => new { sourceText = src, translatedText = tr };


        public YandexTranslateService(IConfiguration config)
        {
            YandexKey = config["YandexKey"];
        }

        public async Task<IEnumerable<string>> Translate(IEnumerable<string> strings)
        {
            var response = await URL
                .WithHeader("Authorization", "Api-Key " + YandexKey)
                .PostJsonAsync(new
                {
                    sourceLanguageCode = "fr",
                    targetLanguageCode = "ru",
                    texts = strings,
                    glossaryConfig = Glossary
                });

            var result = await response.GetJsonAsync<TranslateResult>();
            return result.Translations.Select(t => t.Text);
        }

        public class TranslateResult
        {
            public IEnumerable<TranslateRow> Translations { get; set; }
        }

        public class TranslateRow
        {
            public string Text { get; set; }
        }
    }
}
