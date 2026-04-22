using WebApplication2.DTOs;

namespace WebApplication2.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> getAllAppointmentsAsync();
}