using System.Data;
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

    public async Task<IEnumerable<AppointmentListDto>> getAllAppointmentsAsync(string? status=null,string? patientLastName = null)
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
            WHERE (@Status IS NULL OR a.Status = @Status)
                AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
            ORDER BY a.AppointmentDate";
        
        var appointments = new List<AppointmentListDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand();
        command.Connection=connection;
        command.CommandText = query;
        command.Parameters.Add("@Status", SqlDbType.NVarChar,50).Value = (object?) status ?? DBNull.Value;
        command.Parameters.Add("@PatientLastName", SqlDbType.NVarChar,100).Value = (object?) patientLastName ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync();
       
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

    public async Task<AppointmentDetailsDTO> getAppointmentByIdAsync(int idAppointment)
    {

        var query =
            @"SELECT a.IdAppointment,a.AppointmentDate, a.Status,a.Reason,p.FirstName + ' ' + p.LastName AS PatientFullName,p.Email AS PatientEmail,
                     p.Phone AS PatientPhone, d.FirstName + ' ' + d.LastName AS DoctorFullName, d.LicenseNumber AS DoctorLicenseNumber, s.Name as SpecializationName, a.InternalNotes,a.CreatedAt
                     FROM Appointments a
                     JOIN Patients p ON p.IdPatient = a.IdPatient
                     JOIN  Doctors d ON d.IdDoctor = a.IdDoctor
                     JOIN  Specializations s ON d.IdSpecialization = s.IdSpecialization
                     WHERE A.idAppointment = @idAppointment";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query,connection);
        command.Parameters.Add("@idAppointment", SqlDbType.Int).Value = idAppointment;
        await using var reader = await command.ExecuteReaderAsync();
        if(!await reader.ReadAsync())
            return null;
        return new AppointmentDetailsDTO()
        {
            IdAppointment = reader.GetInt32(0),
            AppointmentDate = reader.GetDateTime(1),
            Status = reader.GetString(2),
            Reason = reader.GetString(3),
            PatientFullName = reader.GetString(4),
            PatientEmail = reader.GetString(5),
            PatientPhone = reader.GetString(6),
            DoctorFullName = reader.GetString(7),
            DoctorLicense = reader.GetString(8),
            SpecializationName = reader.GetString(9),
            InternalNotes = reader.IsDBNull(10) ? null : reader.GetString(10),
            CreatedAt = reader.GetDateTime(11)
        };
    }
}