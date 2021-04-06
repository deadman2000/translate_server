using Microsoft.AspNetCore.Mvc;

namespace TranslateServer.Controllers
{
    public class ApiController : ControllerBase
    {
        protected ActionResult ApiBadRequest(string message)
        {
            return BadRequest(new { Message = message });
        }
    }
}
