using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Google_Calendar_Service
{
    public class SqlCalendarRepository : ICalendarRepository
    {
        private readonly string _connectionString;
        private readonly IErrorNotifier _notifier;

        public SqlCalendarRepository(IErrorNotifier notifier)
        {
            _notifier = notifier;
            _connectionString = ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString;
        }

        public async Task<string[]> GetCalendarNamesAsync()
        {
            try
            {
                var logNameList = new List<string>();
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand("SELECT calendarName FROM tbl_calendarsNames", connection))
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                logNameList.Add(reader.GetString(0));
                            }
                        }
                    }
                }
                return logNameList.ToArray();
            }
            catch (Exception ex)
            {
                await _notifier.NotifyAsync(ex);
                return Array.Empty<string>();
            }
        }

        public async Task UpsertCalendarNamesAsync(IEnumerable<string> calendarNames)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    foreach (string summary in calendarNames)
                    {
                        SqlCommand selectCommand = new SqlCommand("SELECT COUNT(*) FROM [tbl_calendarsNames] WHERE [calendarName] = @summary", connection);
                        selectCommand.Parameters.AddWithValue("@summary", summary);
                        int count = (int)await selectCommand.ExecuteScalarAsync();

                        if (count == 0)
                        {
                            SqlCommand insertCommand = new SqlCommand("INSERT INTO [tbl_calendarsNames] ([calendarName]) VALUES (@summary)", connection);
                            insertCommand.Parameters.AddWithValue("@summary", summary);
                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _notifier.NotifyAsync(ex);
            }
        }

        public async Task ProcessEventAsync(Event eventItem, string calendarName)
        {
            try
            {
                string startDateString = eventItem.Start?.Date ?? eventItem.Start?.DateTimeRaw ?? DateTime.Today.ToString("yyyy-MM-dd");
                DateTime startDate;
                DateTime start = DateTime.UtcNow;
                if (DateTime.TryParseExact(startDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDate))
                {
                    start = new DateTime(startDate.Year, startDate.Month, startDate.Day, startDate.Hour, startDate.Minute, startDate.Second);
                }
                else if (DateTime.TryParseExact(startDateString, "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDate))
                {
                    start = new DateTime(startDate.Year, startDate.Month, startDate.Day, startDate.Hour, startDate.Minute, startDate.Second);
                }

                string endDateString = eventItem.End?.Date ?? eventItem.End?.DateTimeRaw ?? DateTime.Now.ToString("yyyy-MM-dd");
                DateTime endDate;
                DateTime end = DateTime.UtcNow;
                if (DateTime.TryParseExact(endDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate))
                {
                    end = new DateTime(endDate.Year, endDate.Month, endDate.Day, endDate.Hour, endDate.Minute, endDate.Second);
                }
                else if (DateTime.TryParseExact(endDateString, "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate))
                {
                    end = new DateTime(endDate.Year, endDate.Month, endDate.Day, endDate.Hour, endDate.Minute, endDate.Second);
                }

                DateTime now = DateTime.Now;
                if (start < now.AddDays(-14) || end > now.AddDays(14))
                {
                    return;
                }

                string title = eventItem.Summary;
                int studyTypeID = -1;
                if (title != null)
                {
                    using (SqlConnection connection = new SqlConnection(_connectionString))
                    {
                        SqlCommand command = new SqlCommand("SELECT ID FROM StudyType WHERE StudyTypeName = @StudyTypeName", connection);
                        command.Parameters.AddWithValue("@StudyTypeName", title);
                        await connection.OpenAsync();
                        object result = await command.ExecuteScalarAsync();
                        if (result != DBNull.Value && result != null)
                        {
                            studyTypeID = (int)result;
                        }
                    }
                }
                if (studyTypeID == -1) return;

                int userID = -1;
                if (!string.IsNullOrEmpty(calendarName))
                {
                    using (SqlConnection connection = new SqlConnection(_connectionString))
                    {
                        SqlCommand command = new SqlCommand("SELECT UserID FROM tbl_calendarsNames WHERE calendarName = @calendarName", connection);
                        command.Parameters.AddWithValue("@calendarName", calendarName);
                        await connection.OpenAsync();
                        object result = await command.ExecuteScalarAsync();
                        if (result != DBNull.Value && result != null)
                        {
                            userID = Convert.ToInt32(result);
                        }
                    }
                }
                if (userID == -1) return;

                List<int> studentIDs = new List<int>();
                if (eventItem.Attendees != null)
                {
                    string[] participantNames = eventItem.Attendees.Select(a => a.DisplayName).ToArray();
                    if (participantNames != null)
                    {
                        foreach (string participantName in participantNames)
                        {
                            if (participantName != null)
                            {
                                string[] nameParts = participantName.Split(' ');
                                string firstName = nameParts[0];
                                string lastName = nameParts.Length > 1 ? nameParts[1] : "";
                                using (SqlConnection connection = new SqlConnection(_connectionString))
                                {
                                    SqlCommand command = new SqlCommand("SELECT ID FROM Students WHERE FirstName LIKE @firstName AND LastName LIKE @lastName", connection);
                                    command.Parameters.AddWithValue("@firstName", "%" + firstName + "%");
                                    command.Parameters.AddWithValue("@lastName", "%" + lastName + "%");
                                    await connection.OpenAsync();
                                    object result = await command.ExecuteScalarAsync();
                                    if (result != DBNull.Value && result != null)
                                    {
                                        studentIDs.Add((int)result);
                                    }
                                }
                            }
                        }
                    }
                }

                string selectQuery = "SELECT COUNT(*) FROM Lessons WHERE " +
                                     "StudyType = @StudyType AND " +
                                     "StartTime = @StartTime AND " +
                                     "EndTime = @EndTime AND " +
                                     "DayOfWeek = @DayOfWeek AND " +
                                     "User = @User AND " +
                                 "Student = @Student AND " +
                                 "Archive = @Archive AND " +
                                 "StudyDay = @StudyDay AND " +
                                 "Eid = @Eid";

            string insertQueryLessons = "INSERT INTO Lessons (StudyType, StartTime, EndTime, DayOfWeek, User, Student, Archive, StudyDay, Eid) " +
                                        "VALUES (@StudyType, @StartTime, @EndTime, @DayOfWeek, @User, @Student, @Archive, @StudyDay, @Eid);";

            string selectId = "SELECT ID FROM Lessons WHERE " +
                              "Eid = @Eid AND " +
                              "StudyType = @StudyType AND " +
                              "StartTime = @StartTime AND " +
                              "EndTime = @EndTime AND " +
                              "DayOfWeek = @DayOfWeek AND " +
                              "User = @User AND " +
                              "Student = @Student AND " +
                              "Archive = @Archive AND " +
                              "StudyDay = @StudyDay";

            int dayOfWeek = (int)start.DayOfWeek + 1;
            string formattedDayOfWeek = $";{dayOfWeek};";
            string studentIDsString = ";" + string.Join(";", studentIDs) + ";";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand checkCommand = new SqlCommand(selectQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@StudyType", studyTypeID);
                    checkCommand.Parameters.AddWithValue("@StartTime", start);
                    checkCommand.Parameters.AddWithValue("@EndTime", end);
                    checkCommand.Parameters.AddWithValue("@DayOfWeek", formattedDayOfWeek);
                    checkCommand.Parameters.AddWithValue("@User", userID);
                    checkCommand.Parameters.AddWithValue("@Student", studentIDsString);
                    checkCommand.Parameters.AddWithValue("@Archive", false);
                    checkCommand.Parameters.AddWithValue("@StudyDay", "Regular");
                    checkCommand.Parameters.AddWithValue("@Eid", eventItem.Id);

                    await connection.OpenAsync();
                    int count = (int)await checkCommand.ExecuteScalarAsync();
                    connection.Close();

                    if (count == 0)
                    {
                        using (SqlCommand insertCommand = new SqlCommand(insertQueryLessons, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@StudyType", studyTypeID);
                            insertCommand.Parameters.AddWithValue("@StartTime", start);
                            insertCommand.Parameters.AddWithValue("@EndTime", end);
                            insertCommand.Parameters.AddWithValue("@DayOfWeek", formattedDayOfWeek);
                            insertCommand.Parameters.AddWithValue("@User", userID);
                            insertCommand.Parameters.AddWithValue("@Student", studentIDsString);
                            insertCommand.Parameters.AddWithValue("@Archive", false);
                            insertCommand.Parameters.AddWithValue("@StudyDay", "Regular");
                            insertCommand.Parameters.AddWithValue("@Eid", eventItem.Id);

                            await connection.OpenAsync();
                            await insertCommand.ExecuteNonQueryAsync();
                            connection.Close();
                        }

                        int id;
                        using (SqlCommand selectCommand = new SqlCommand(selectId, connection))
                        {
                            selectCommand.Parameters.AddWithValue("@StudyType", studyTypeID);
                            selectCommand.Parameters.AddWithValue("@StartTime", start);
                            selectCommand.Parameters.AddWithValue("@EndTime", end);
                            selectCommand.Parameters.AddWithValue("@DayOfWeek", formattedDayOfWeek);
                            selectCommand.Parameters.AddWithValue("@User", userID);
                            selectCommand.Parameters.AddWithValue("@Student", studentIDsString);
                            selectCommand.Parameters.AddWithValue("@Archive", false);
                            selectCommand.Parameters.AddWithValue("@StudyDay", "Regular");
                            selectCommand.Parameters.AddWithValue("@Eid", eventItem.Id);

                            await connection.OpenAsync();
                            id = (int)await selectCommand.ExecuteScalarAsync();
                            connection.Close();
                        }

                        if (studentIDs.Count != 0)
                        {
                            foreach (int sid in studentIDs)
                            {
                                for (int option = 8; option <= 10; option++)
                                {
                                    string insertSS = "INSERT INTO [dbo].[StudentStudies] ([Study], [Option], [Status], [Student], [User], [Date], [Absent]) " +
                                                      "VALUES (@study, @option, @status, @student, @user, @date, @absent);";
                                    string selectSS = "SELECT COUNT(*) FROM [dbo].[StudentStudies] WHERE [Study] = @study AND [Option] = @option AND [Status] = @status AND [Student] = @student AND [User] = @user AND [Absent] = @absent";

                                    using (SqlCommand selectCommand = new SqlCommand(selectSS, connection))
                                    {
                                        selectCommand.Parameters.AddWithValue("@study", id);
                                        selectCommand.Parameters.AddWithValue("@option", option);
                                        selectCommand.Parameters.AddWithValue("@status", 1);
                                        selectCommand.Parameters.AddWithValue("@student", sid);
                                        selectCommand.Parameters.AddWithValue("@user", userID);
                                        selectCommand.Parameters.AddWithValue("@date", start);
                                        selectCommand.Parameters.AddWithValue("@absent", 0);

                                        await connection.OpenAsync();
                                        int count1 = (int)selectCommand.ExecuteScalar();
                                        connection.Close();

                                        if (count1 == 0)
                                        {
                                            using (SqlCommand insertCommand = new SqlCommand(insertSS, connection))
                                            {
                                                insertCommand.Parameters.AddWithValue("@study", id);
                                                insertCommand.Parameters.AddWithValue("@option", option);
                                                insertCommand.Parameters.AddWithValue("@status", 1);
                                                insertCommand.Parameters.AddWithValue("@student", sid);
                                                insertCommand.Parameters.AddWithValue("@user", userID);
                                                insertCommand.Parameters.AddWithValue("@date", start);
                                                insertCommand.Parameters.AddWithValue("@absent", 0);

                                                await connection.OpenAsync();
                                                await insertCommand.ExecuteNonQueryAsync();
                                                connection.Close();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await _notifier.NotifyAsync(ex);
        }

    public async Task DeleteDeletedEventsAsync(CalendarService service)
    {
        try
        {
            CalendarList calendars = await service.CalendarList.List().ExecuteAsync();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                foreach (CalendarListEntry calendar in calendars.Items)
                {
                    EventsResource.ListRequest request = service.Events.List(calendar.Id);
                    request.ShowDeleted = true;
                    request.TimeMin = DateTimeOffset.Now.AddDays(-90).ToUniversalTime().DateTime;
                    request.TimeMax = DateTimeOffset.Now.AddDays(90).ToUniversalTime().DateTime;
                    Events events = await request.ExecuteAsync();

                    List<int> deletedRecordIds = new List<int>();
                    List<int> deleteduserID = new List<int>();
                    List<DateTime> deletedDates = new List<DateTime>();

                    foreach (Event eventItem in events.Items)
                    {
                        if (eventItem.Status == "cancelled")
                        {
                            await connection.OpenAsync();
                            SqlCommand command0 = new SqlCommand("SELECT [User], [ID], [LessonTime] FROM [dbo].[Studies] WHERE Eid = @Eid", connection);
                            command0.Parameters.AddWithValue("@Eid", eventItem.Id);
                            SqlDataReader reader = await command0.ExecuteReaderAsync();

                            int userId = -1;
                            int recordId = -1;
                            DateTime lessonTime = DateTime.MinValue;

                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    userId = (int)reader["User"];
                                    recordId = (int)reader["ID"];
                                    lessonTime = (DateTime)reader["LessonTime"];
                                }
                            }

                            reader.Close();
                            connection.Close();

                            if (lessonTime != DateTime.MinValue && recordId != -1 && userId != -1)
                            {
                                deletedRecordIds.Add(recordId);
                                deleteduserID.Add(userId);
                                deletedDates.Add(lessonTime);
                                await connection.OpenAsync();
                                SqlCommand command = new SqlCommand("DELETE FROM [dbo].[Studies] WHERE Eid = @Eid", connection);
                                command.Parameters.AddWithValue("@Eid", eventItem.Id);
                                await command.ExecuteNonQueryAsync();
                                connection.Close();
                            }
                        }
                    }

                    if (deletedRecordIds.Count > 0)
                    {
                        using (SqlConnection connection1 = new SqlConnection(_connectionString))
                        {
                            await connection1.OpenAsync();

                            for (int i = 0; i < deletedRecordIds.Count && i < deleteduserID.Count && i < deletedDates.Count; i++)
                            {
                                int recordId = deletedRecordIds[i];
                                int userId = deleteduserID[i];
                                DateTime lessonTime = deletedDates[i];

                                string queryString = "UPDATE [dbo].[StudentStudies] SET [Absent] = 1 WHERE [Date] = @lessonTime AND [Study] = @recordId AND [User] = @userId";
                                using (SqlCommand command = new SqlCommand(queryString, connection1))
                                {
                                    command.Parameters.AddWithValue("@lessonTime", lessonTime);
                                    command.Parameters.AddWithValue("@recordId", recordId);
                                    command.Parameters.AddWithValue("@userId", userId);

                                    int rowsAffected = command.ExecuteNonQuery();
                                    Console.WriteLine("Rows affected: " + rowsAffected);
                                }
                            }

                            connection1.Close();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await _notifier.NotifyAsync(ex);
        }
    }
    }
}
