using Microsoft.AspNetCore.Mvc;
using WebApplication2.DTOs;
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
    public async Task<IActionResult> GetAppointments([FromQuery] string? status,[FromQuery] string? patientLastName)
    {
        var result = await _service.getAllAppointmentsAsync(status, patientLastName);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAppointmentById([FromRoute] int id)
    {
        var appointment = await _service.getAppointmentByIdAsync(id);
        if (appointment is null)
        {
            return NotFound();
        }
        return Ok(appointment);
        
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointmentAsync([FromBody] CreateAppointmentRequestDto dto)
    {
        try
        {
            var newId = await _service.CreateAppointmentAsync(dto);
            return CreatedAtAction(nameof(GetAppointmentById), new { id = newId }, null);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = ex.Message });

            return BadRequest(new { message = ex.Message });
        }
    }
}