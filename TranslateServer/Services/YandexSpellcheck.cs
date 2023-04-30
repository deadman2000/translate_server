using Flurl.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using TranslateServer.Model.Yandex;

namespace TranslateServer.Services
{
    public class YandexSpellcheck
    {
        private readonly string CHECKTEXT_URL = "https://speller.yandex.net/services/spellservice.json/checkText";

        public async Task<SpellResult[]> Spellcheck(string text)
        {
            while (true)
            {
                try
                {
                    var response = await CHECKTEXT_URL.PostUrlEncodedAsync(new { text, lang = "ru" });
                    return await response.GetJsonAsync<SpellResult[]>();
                }
                catch (FlurlHttpException fhex)
                {
                    System.Console.WriteLine(fhex);
                }
            }
        }

        public async IAsyncEnumerable<SpellResult[]> Spellcheck(string[] texts)
        {
            foreach (var txt in texts)
            {
                yield return await Spellcheck(txt);
            }
        }
    }
}
