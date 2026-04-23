using System.Data;
using System.Xml;
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

    public async Task UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var checkAppointmentCmd = new SqlCommand(
            "SELECT Status, IdDoctor, AppointmentDate FROM Appointments WHERE IdAppointment = @Id",
            connection);
        checkAppointmentCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        string? currentStatus = null;
        int currentDoctorID = 0;
        DateTime originalAppointmentDate = default;

        await using (var reader = await checkAppointmentCmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
                throw new Exception("NOT_FOUND");
            currentStatus = reader.GetString(0);
            currentDoctorID = reader.GetInt32(1);
        }

        var checkPatientCmd = new SqlCommand("SELECT COUNT(*) FROM Patients WHERE IdPatient = @Id AND IsActive = 1",
            connection);
        checkPatientCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdPatient;
        if ((int)await checkPatientCmd.ExecuteScalarAsync() == 0)
            throw new Exception("INVALID_PATIENT");
        var checkDoctorCmd = new SqlCommand(
            "SELECT COUNT(*) FROM Doctors WHERE IdDoctor = @Id AND IsActive = 1",
            connection);
        checkDoctorCmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.IdDoctor;
        if ((int)await checkDoctorCmd.ExecuteScalarAsync() == 0)
            throw new Exception($"INVALID_DOCTOR");

        var allowedStatuses = new[] { "Scheduled", "Completed", "Cancelled" };
        if(!allowedStatuses.Contains(dto.Status))
            throw new Exception("INVALID_STATUS");

        if (currentStatus == "Completed" && dto.AppointmentDate != originalAppointmentDate)
        {
            throw new Exception("CANNOT_CHANGE_DATE");
        }
        var conflictCmd = new SqlCommand(
            @"SELECT COUNT(*) FROM Appointments
          WHERE IdDoctor = @IdDoctor
            AND AppointmentDate = @Date
            AND IdAppointment <> @IdAppointment",
            connection);

        conflictCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        conflictCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        conflictCmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
        if ((int)await conflictCmd.ExecuteScalarAsync() > 0)
        {
            throw new Exception("CONFLICT");
        }
        if(dto.Reason.Length>250)
        {
            throw new Exception("INVALID_REASON");
        }

        if (dto.InternalNotes != null && dto.InternalNotes.Length > 500)
        {
            throw new Exception("INVALID_NOTES");
        }
        
        var updateCmd = new SqlCommand(
            @"UPDATE Appointments
          SET IdPatient = @IdPatient,
              IdDoctor = @IdDoctor,
              AppointmentDate = @Date,
              Status = @Status,
              Reason = @Reason,
              InternalNotes = @Notes
          WHERE IdAppointment = @Id",
            connection);

        updateCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
        updateCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
        updateCmd.Parameters.Add("@Date", SqlDbType.DateTime2).Value = dto.AppointmentDate;
        updateCmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = dto.Status;
        updateCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
        updateCmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value = (object?)dto.InternalNotes ?? DBNull.Value;
        updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;

        await updateCmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var checkCmd = new SqlCommand(
            "SELECT Status FROM Appointments WHERE IdAppointment = @Id",
            connection);
        checkCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        var statusObj = await checkCmd.ExecuteScalarAsync();
        if (statusObj == null)
        {
            throw new Exception("NOT_FOUND");
        }
        var status = (string)statusObj;
        if (status == "Completed")
        {
            throw new Exception("CANNOT_DELETE_COMPLETED");
        }
        var deleteCmd = new SqlCommand(
            "DELETE FROM Appointments WHERE IdAppointment = @Id",
            connection);
        deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
        
        await deleteCmd.ExecuteNonQueryAsync();
    }
}