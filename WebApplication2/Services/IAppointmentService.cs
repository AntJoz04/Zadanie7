using WebApplication2.DTOs;

namespace WebApplication2.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> getAllAppointmentsAsync(string? status=null,string? patientLastName = null);
    Task<AppointmentDetailsDTO?> getAppointmentByIdAsync(int idAppointment);
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto);


}