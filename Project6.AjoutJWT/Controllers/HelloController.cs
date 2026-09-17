using Microsoft.AspNetCore.Mvc;

namespace Project6.AjoutJWT.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HelloController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> SayHello()
    {
        return Ok(new { Message = "Hello world"} );
    }
}