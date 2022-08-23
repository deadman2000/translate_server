using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ApiController
    {
        private readonly SearchService _elastic;

        public SearchController(SearchService elastic)
        {
            _elastic = elastic;
        }

        public class SearchRequest
        {
            public string Query { get; set; }
            public bool Source { get; set; } = true;
            public bool Translated { get; set; } = true;
        }

        [HttpPost]
        public async Task<ActionResult> Search(SearchRequest request)
        {
            var result = await _elastic.Search(request.Query, request.Source, request.Translated);
            return Ok(new
            {
                request.Query,
                result
            });
        }
    }
}
