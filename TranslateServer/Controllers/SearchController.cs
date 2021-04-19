using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TranslateServer.Services;

namespace TranslateServer.Controllers
{
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
        }

        [HttpPost]
        public async Task<ActionResult> Search(SearchRequest request)
        {
            var result = await _elastic.Search(request.Query);
            return Ok(result);
        }
    }
}
