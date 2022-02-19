using Microsoft.AspNetCore.Mvc;

namespace TranslateServer.Controllers
{
    [Route("")]
    [ApiController]
    public class IndexController : ControllerBase
    {
        [HttpGet]
        public ActionResult Index() { return Ok(); }
    }
}
