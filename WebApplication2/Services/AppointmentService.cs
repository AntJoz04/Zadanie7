using Microsoft.Data.SqlClient;
using WebApplication2.DTOs;

namespace WebApplication2.Services;

public class AppointmentService : IAppointmentService
{
    private readonly string _connectionString;

    public AppointmentService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<IEnumerable<AppointmentListDto>> getAllAppointmentsAsync()
    {
        var query = @"
         SELECT
            a.IdAppointment,
            a.AppointmentDate,
            a.Status,
            a.Reason,
            p.FirstName + ' ' + p.LastName AS PatientFullName,
            p.Email
            FROM Appointments a
            JOIN Patients p ON p.IdPatient = a.IdPatient
            ORDER BY a.AppointmentDate";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection=connection;
        command.CommandText = query;
        await using var reader = await command.ExecuteReaderAsync();
        var appointments = new List<AppointmentListDto>();
        while (await reader.ReadAsync())
        {
            var appointment = new AppointmentListDto()
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            };
            appointments.Add(appointment);
        }
        return appointments;
    }
}