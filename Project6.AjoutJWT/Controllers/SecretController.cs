using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project6.AjoutJWT.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class SecretController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> SayHello()
    {
        return Ok(new { Message = "Hello world"} );
    }
}