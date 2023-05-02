using Microsoft.AspNetCore.Mvc;

namespace helloservice.Controllers;

[ApiController]
[Route("[controller]")]
public class GreetController : ControllerBase
{
    [HttpGet("/hello")]
    public IActionResult Test() => Ok("Hello world!");
}
