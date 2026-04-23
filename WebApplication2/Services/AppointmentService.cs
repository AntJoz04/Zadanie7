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
                     p.PhoneNumber AS PhoneNumber, d.FirstName + ' ' + d.LastName AS DoctorFullName, d.LicenseNumber AS DoctorLicenseNumber, s.Name as SpecializationName, a.InternalNotes,a.CreatedAt
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

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var checkPatientcmd = new SqlCommand("SELECT COUNT(*) FROM PATIENTS WHERE IDPATIENT = @Id and IsActive = 1",
            connection);
        checkPatientcmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdPatient;
        var patientExists = (int) await checkPatientcmd.ExecuteScalarAsync()>0;
        if (!patientExists)
        {
            throw new Exception("Patient does not exist");
        }

        var checkDoctorCmd = new SqlCommand("SELECT COUNT(*) FROM Doctors WHERE IdDoctor = @Id and IsActive =1",connection);
        checkDoctorCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdDoctor;
        var doctorExists = (int)await checkDoctorCmd.ExecuteScalarAsync() > 0;
        if (!doctorExists)
        {
            throw new Exception("Doctor does not exists");
        }

        if (dto.AppointmentDate < DateTime.UtcNow)
        {
            throw new Exception("WRONG DATE");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 250)
        {
            throw new Exception("Invalid Reason");
        }
        var conflictCMD = new SqlCommand(
            @"SELECT COUNT(*) FROM Appointments 
          WHERE IdDoctor = @IdDoctor AND AppointmentDate = @Date",
            connection);
        conflictCMD.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        conflictCMD.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        var conflict = (int) await conflictCMD.ExecuteScalarAsync() > 0;
        if (conflict)
        {
            throw new Exception("Conflict");
            
            
        }
        var insertCmd = new SqlCommand(
            @"INSERT INTO Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
          VALUES (@IdPatient, @IdDoctor, @Date, 'Scheduled', @Reason);
          SELECT SCOPE_IDENTITY();",
            connection);

        insertCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        insertCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        insertCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        insertCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
        var newId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
        return newId;
    }
}