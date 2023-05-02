using Microsoft.AspNetCore.Mvc;

namespace goodbyeservice.Controllers;

[ApiController]
[Route("[controller]")]
public class GoodByeController : ControllerBase
{
    [HttpGet("/bye")]
    public IActionResult Test() => Ok("Goodbye!");
}
