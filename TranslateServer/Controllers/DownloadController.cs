using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Route("api/projects/{project}/[controller]")]
    [ApiController]
    public class DownloadController : ApiController
    {
        private readonly SCIService _sci;
        private readonly TranslateService _translate;
        private readonly PatchesService _patches;

        public DownloadController(SCIService sci, TranslateService translate, PatchesService patches)
        {
            _sci = sci;
            _translate = translate;
            _patches = patches;
        }

        [HttpGet("patch")]
        public async Task Patch(string project)
        {
            var texts = await _translate.Query(t => t.Project == project && !t.Deleted);
            var package = _sci.Load(project);

            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var g in texts.GroupBy(t => t.Volume))
                {
                    var resourceName = g.Key.Replace('_', '.');
                    var res = package.GetResource(resourceName);
                    var strings = res.GetStrings();
                    foreach (var t in g)
                        strings[t.Number] = t.Text;

                    res.SetStrings(strings);
                    var bytes = res.GetPatch();
                    var entry = archive.CreateEntry(resourceName);
                    using var s = entry.Open();
                    res.Save(s, bytes);
                }
            }

            ms.Seek(0, SeekOrigin.Begin);
            Response.StatusCode = 200;
            Response.Headers.Add(HeaderNames.ContentDisposition, $"attachment; filename=\"{project}.patch.zip\"");
            Response.Headers.Add(HeaderNames.ContentType, "application/octet-stream");
            await ms.CopyToAsync(Response.Body);
        }
    }
}
