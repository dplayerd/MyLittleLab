using Microsoft.AspNetCore.Mvc;
using LoggingDemo.Logging;

namespace LoggingDemo.Controllers;

[ApiController]
[Route("logs")]
public class LogsController : ControllerBase
{
    //[HttpGet("info")]
    //public IActionResult GetInfoLogs() => Ok(MemoryLogData.InfoLogs);

    //[HttpGet("warning")]
    //public IActionResult GetWarningLogs() => Ok(MemoryLogData.WarningLogs);

    //[HttpGet("error")]
    //public IActionResult GetErrorLogs() => Ok(MemoryLogData.ErrorLogs);

    [HttpGet]
    public IActionResult Get(LogLevel lvl)
    {
        switch (lvl)
        {
            case LogLevel.Trace:
                return Ok(MemoryLogData.TraceLogs);

            case LogLevel.Debug:
                return Ok(MemoryLogData.DebugLogs);

            case LogLevel.Information:
                return Ok(MemoryLogData.InfoLogs);

            case LogLevel.Warning:
                return Ok(MemoryLogData.WarningLogs);

            case LogLevel.Error:
                return Ok(MemoryLogData.ErrorLogs);

            default:
                return Ok(Array.Empty<string>());
        }
    }
}
