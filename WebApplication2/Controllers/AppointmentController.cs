using Microsoft.AspNetCore.Mvc;
using WebApplication2.Services;

namespace WebApplication2.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AppointmentController : ControllerBase
{
    private readonly IAppointmentService _service;

    public AppointmentController(IAppointmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments()
    {
        var result = await _service.getAllAppointmentsAsync();
        return Ok(result);
    }
}