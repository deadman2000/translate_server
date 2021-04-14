using Microsoft.AspNetCore.Mvc;

namespace TranslateServer.Controllers
{
    public class ApiController : ControllerBase
    {
        protected string UserLogin => User.Identity.Name;

        protected ActionResult ApiBadRequest(string message)
        {
            return BadRequest(new { Message = message });
        }
    }
}
