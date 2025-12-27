using Flurl.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Model.Yandex;

namespace TranslateServer.Services
{
    public class YandexSpellcheck
    {
        private readonly string CHECKTEXT_URL = "https://speller.yandex.net/services/spellservice.json/checkText";

        public async Task<IEnumerable<SpellResult>> Spellcheck(string text)
        {
            //return Array.Empty<SpellResult>();
            while (true)
            {
                try
                {
                    var response = await CHECKTEXT_URL.PostUrlEncodedAsync(new { text, lang = "ru" });
                    var results = await response.GetJsonAsync<SpellResult[]>();
                    foreach (var res in results)
                        res.S = res.S.Where(s => s.Trim() != res.Word.Trim());
                    return results.Where(r => r.S.Any());
                }
                catch (FlurlHttpException fhex)
                {
                    System.Console.WriteLine(fhex);
                }
            }
        }

        public async IAsyncEnumerable<IEnumerable<SpellResult>> Spellcheck(string[] texts)
        {
            foreach (var txt in texts)
            {
                yield return await Spellcheck(txt);
            }
        }
    }
}
