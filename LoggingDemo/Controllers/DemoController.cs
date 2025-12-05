using LoggingDemo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;

namespace LoggingDemo.Controllers;

[ApiController]
[Route("demo")]
public class DemoController : ControllerBase
{
    private readonly ILogger<DemoController> _logger;
    private readonly FakeBusinessService _service;

    public DemoController(ILogger<DemoController> logger, FakeBusinessService service)
    {
        this._logger = logger;
        this._service = service;
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        this._logger.LogTrace("This is an info log. of DemoController");

        using (this._logger.BeginScope("ScopeId:{ScopeId}", Guid.NewGuid()))
        {
            this._service.PerformOperation();   
        }

        return Ok("Logs written to memory");
    }
}
