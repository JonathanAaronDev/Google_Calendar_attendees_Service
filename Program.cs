using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Timer = System.Timers.Timer;


namespace Google_Calendar_Service
{

    class Program
    {
        // Declare the timer and logNames variables
        private static Timer timer;
        private static string[] logNames;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1); // SemaphoreSlim to synchronize access

        // Define the connection string to the SQL Server database
        //private static string connectionString = ConfigurationManager.ConnectionStrings["MyDbConnection"].ConnectionString;

        // private static string connectionString = "Data Source=DESKTOP-IQUC79O\\SQLEXPRESS;Initial Catalog=matereuven;Integrated Security=True;";
        // private static string connectionString = "Data Source=DESKTOP-OAR6QIG\\SQLEXPRESS;Initial Catalog=matereuven;Integrated Security=True;";
        private static string connectionString = "Data Source=SERVER123\\SQLINSTANCE;Initial Catalog=myRandomDB;Integrated Security=True;";


        static string ReadConnectionStringFromFile(string fileName)
        {
            // Get the parent directory of the current directory (i.e., "bin\Debug")
            string parentDir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;

            // Construct the full file path
            string filePath = Path.Combine(parentDir, fileName);

            // Read the text file and return the content as a string
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (IOException ex)
            {
                using (StreamWriter writer = File.AppendText("error_log.txt"))
                {
                    // Get the current date and time
                    DateTime now = DateTime.Now;
                    // Format the date and time as a string
                    string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
                    // Write the timestamp and the exception message to the file
                    writer.WriteLine("Error occurred at {0}: {1}", timestamp, ex.Message);
                    // Append a newline after the entry
                    writer.WriteLine();
                }
                return string.Empty; // return an empty string or throw an exception as appropriate for your use case
            }
        }



        static async Task Main(string[] args) // Update the Main method to return a Task for async operations
        {
            while (true)
            {
                try
                {
                    // Get the log names from the database asynchronously
                    List<string> logNameList = new List<string>();
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync(); // Use OpenAsync method for asynchronous database connection
                                                      // Execute a SELECT query asynchronously to retrieve the calendarName column values from tbl_calendarsNames table
                        using (SqlCommand command = new SqlCommand("SELECT calendarName FROM tbl_calendarsNames", connection))
                        using (SqlDataReader reader = await command.ExecuteReaderAsync()) // Use ExecuteReaderAsync method for asynchronous data retrieval
                        {
                            while (await reader.ReadAsync()) // Use ReadAsync method for asynchronous reading of data
                            {
                                if (!reader.IsDBNull(0)) // Check if the value is not null
                                {
                                    // Add the calendarName values to the logNameList
                                    logNameList.Add(reader.GetString(0));
                                }
                            }
                        }
                    }

                    // Convert the logNameList to an array and assign it to the logNames variable
                    logNames = logNameList.ToArray();

                    var timer = new System.Timers.Timer(1 * 1 * 10 * 1000); // 8 hours in milliseconds 


                    timer.Elapsed += Timer_Elapsed;
                    timer.Start();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();

                    // Dispose of the timer object
                    timer.Elapsed -= Timer_Elapsed; // Unsubscribe from the event
                    timer.Stop(); // Stop the timer

                    // Restart the loop
                    continue;
                }
                catch (Exception ex)
                {
                    timer.Elapsed -= Timer_Elapsed; // Unsubscribe from the event
                    timer.Stop(); // Stop the timer
                    Console.WriteLine("Error occurred: {0}", ex.Message);
                    using (StreamWriter writer = File.AppendText("error_log.txt"))
                    {
                        // Get the current date and time
                        DateTime now = DateTime.Now;
                        // Format the date and time as a string
                        string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
                        // Write the timestamp and the exception message to the file
                        writer.WriteLine("Error occurred at {0}: {1}", timestamp, ex.Message);
                        // Append a newline after the entry
                        writer.WriteLine();
                    }
                    // Restart the loop
                    continue;
                }
            }
        }

        private static async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // If semaphore is not available, return immediately
            if (!semaphore.Wait(0))
            {
                return;
            }

            try
            {
                // Stop the timer
                ((System.Timers.Timer)sender).Stop();
                // Create an instance of the class
                Program program = new Program();
                // Execute function
                await program.OnTimerElapsed(sender, e); // Call the async method synchronously
            }
            finally
            {
                ((System.Timers.Timer)sender).Stop();
                // Release the semaphore
                semaphore.Release();
                // Start the timer
                ((System.Timers.Timer)sender).Start();
            }

        }
        private async Task DeleteDeletedEventsFromSqlTable(string connectionString)
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new[] { CalendarService.Scope.Calendar },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("Google_Calendar_Service")).Result;
            }

            // Create the service object using the UserCredential
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Calendarevents",
            });

            // Get the list of calendars for the authenticated user
            CalendarList calendars = await service.CalendarList.List().ExecuteAsync();


            using (SqlConnection connection = new SqlConnection(connectionString))
            {


                // Iterate over each calendar in the list
                foreach (CalendarListEntry calendar in calendars.Items)
                {
                    string name = calendar.Summary;
                    // Get the list of events for the current calendar
                    EventsResource.ListRequest request = service.Events.List(calendar.Id);
                    request.ShowDeleted = true;
                    request.TimeMin = DateTimeOffset.Now.AddDays(-90).ToUniversalTime().DateTime;
                    request.TimeMax = DateTimeOffset.Now.AddDays(90).ToUniversalTime().DateTime;
                    Events events = await request.ExecuteAsync();
                    List<int> deletedRecordIds = new List<int>();
                    List<int> deleteduserID = new List<int>();
                    List<DateTime> deletedDates = new List<DateTime>();

                    // Iterate over each event in the list
                    foreach (Event eventItem in events.Items)
                    {
                        // Check if the event was deleted
                        if (eventItem.Status == "cancelled")
                        {
                            // Select the ID where the Eid is equal to the eventItem.Id
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
                                    // Read the data from the current row
                                    userId = (int)reader["User"];
                                    recordId = (int)reader["ID"];
                                    lessonTime = (DateTime)reader["LessonTime"];
                                }
                            }

                            reader.Close();
                            connection.Close();

                            if (lessonTime != DateTime.MinValue && recordId != -1 && userId != -1)
                            {
                                deletedRecordIds.Add((int)recordId);
                                deleteduserID.Add((int)userId);
                                deletedDates.Add((DateTime)lessonTime);
                                // Delete the corresponding row from the SQL table
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
                        // the list is not empty
                        using (SqlConnection connection1 = new SqlConnection(connectionString))
                        {
                            await connection1.OpenAsync();

                            for (int i = 0; i < deletedRecordIds.Count && i < deleteduserID.Count && i < deletedDates.Count; i++)
                            {
                                int recordId = deletedRecordIds[i];
                                int userId = deleteduserID[i];
                                DateTime lessonTime = deletedDates[i];

                                string queryString = "UPDATE [dbo].[StudentStudies] SET [Absent] = 1 WHERE [Date] = @lessonTime AND [Study] = @recordId AND [User] = @userId";
                                using (SqlCommand command = new SqlCommand(queryString, connection))
                                {
                                    command.Parameters.AddWithValue("@lessonTime", lessonTime);
                                    command.Parameters.AddWithValue("@recordId", recordId);
                                    command.Parameters.AddWithValue("@userId", userId);

                                    connection.Open();
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

        private async Task OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            
            List<CalendarListEntry> calendarList = new List<CalendarListEntry>();
            try
            {
               
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();
                foreach (var resourceName in resourceNames)
                {
                    Console.WriteLine(resourceName);
                }
                

                UserCredential credential;
                using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        new[] { CalendarService.Scope.Calendar },
                        "user",
                        CancellationToken.None,
                        new FileDataStore("Google_Calendar_Service")).Result;
                }

                // Create the service object using the UserCredential
                var service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Calendarevents",
                });

                // Get all the user's calendars
                CalendarListResource.ListRequest listRequest = service.CalendarList.List();
                //listRequest.MaxResults = 1000; // Remove or set to a smaller value to avoid API limit
                CalendarList calendarListObject = await service.CalendarList.List().ExecuteAsync();
                calendarList = calendarListObject.Items.ToList();

                List<string> summaryList = new List<string>();
                foreach (var item in calendarList)
                {
                    string summary = item.Summary;
                    summaryList.Add(summary);
                }
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    foreach (string summary in summaryList)
                    {
                        // Check if the summary value already exists in the [tbl_calendarsNames] table
                        SqlCommand selectCommand = new SqlCommand("SELECT COUNT(*) FROM [tbl_calendarsNames] WHERE [calendarName] = @summary", connection);
                        selectCommand.Parameters.AddWithValue("@summary", summary);
                        int count = (int)await selectCommand.ExecuteScalarAsync();

                        if (count == 0) // Summary doesn't exist in table, so add it
                        {
                            // Create and execute an INSERT query to add a new row with the summary value
                            SqlCommand insertCommand = new SqlCommand("INSERT INTO [tbl_calendarsNames] ([calendarName]) VALUES (@summary)", connection);
                            insertCommand.Parameters.AddWithValue("@summary", summary);
                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }

                    connection.Close();
                }



                // Iterate over each calendar and search for events
                foreach (CalendarListEntry calendar in calendarList)
                {
                    string calendarName = calendar.Summary;
                    // Search for events in the calendar from the start date to the end date
                    EventsResource.ListRequest request = service.Events.List(calendar.Id);
                    request.ShowDeleted = false;
                    request.TimeMin = DateTime.UtcNow.Date;
                    request.TimeMax = DateTime.UtcNow.Date.AddDays(90);


                    Events events = await request.ExecuteAsync();

                    // Process each event in the calendar
                    foreach (var eventItem in events.Items)
                    {
                        // Get the start and end times of the event
                        string startDateString = eventItem.Start?.Date ?? eventItem.Start?.DateTimeRaw ?? DateTime.Today.ToString("yyyy-MM-dd");
                        DateTime startDate;
                        DateTime start= DateTime.UtcNow; 
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
                        if (start >= now.AddDays(-14) && end <= now.AddDays(14))
                        {
                            // Get the title of the eventhow
                            string title = eventItem.Summary;

                            // Find the study type ID for this event
                            int studyTypeID = -1;
                            if (title != null)
                            {
                                using (SqlConnection connection = new SqlConnection(connectionString))
                                {
                                    // Execute a SELECT query to retrieve the ID from StudyType where StudyTypeName matches the event title
                                    SqlCommand command = new SqlCommand("SELECT ID FROM StudyType WHERE StudyTypeName = @StudyTypeName", connection);
                                    command.Parameters.AddWithValue("@StudyTypeName", title);
                                    await connection.OpenAsync();
                                    object result = await command.ExecuteScalarAsync();
                                    if (result != DBNull.Value && result != null)
                                    {
                                        studyTypeID = (int)result;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    connection.Close();

                                }

                            }
                            if (studyTypeID == -1)
                            {
                                continue;
                            }

                            // Find the user ID for this event
                            int userID = -1;
                            // Get the log name from the event item (logname is diaryname)
                            string logName = calendarName;
                            if (!string.IsNullOrEmpty(calendarName))
                            {
                                using (SqlConnection connection = new SqlConnection(connectionString))
                                {
                                    // Execute a SELECT query to retrieve the UserID from tbl_calendarsNames where calendarName matches the event title
                                    SqlCommand command = new SqlCommand("SELECT UserID FROM tbl_calendarsNames WHERE calendarName = @calendarName", connection);
                                    command.Parameters.AddWithValue("@calendarName", calendarName);
                                    await connection.OpenAsync();
                                    object result = await command.ExecuteScalarAsync();
                                    if (result != DBNull.Value && result != null)
                                    {
                                        userID = Convert.ToInt32(result);
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    connection.Close();
                                }
                            }


                            // Find the student IDs for this event
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
                                            // Split the participantName string by space
                                            string[] nameParts = participantName.Split(' ');

                                            // Extract the first name (index 0) and last name (index 1) from the nameParts array
                                            string firstName = nameParts[0];
                                            string lastName = nameParts[1];
                                            using (SqlConnection connection = new SqlConnection(connectionString))
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
                                                connection.Close();

                                            }


                                        }
                                        else
                                        {
                                            continue;
                                        }

                                    }
                                }


                            }



                            string INSERT = "INSERT INTO Lessons (Eid, StudyType, StartTime, EndTime, DayOfWeek, User, Student, Archive, StudyDay)\r\n" +
                "VALUES (@Eid, @StudyType, @StartTime, @EndTime, @DayOfWeek, @User, @Student, @Archive, @StudyDay)\r\n";

                            string selectQuery = "SELECT COUNT(*) FROM Lessons WHERE " +
                                                "Eid = @Eid AND " +
                                                "StudyType = @StudyType AND " +
                                                "StartTime = @StartTime AND " +
                                                "EndTime = @EndTime AND " +
                                                "DayOfWeek = @DayOfWeek AND " +
                                                "User = @User AND " +
                                                "Student = @Student AND " +
                                                "Archive = @Archive AND " +
                                                "StudyDay = @StudyDay";

                            string selectid = "SELECT ID FROM Lessons WHERE " +
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
                            // Create an entry in the study table for each student

                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                // Check if the row exists
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
                                        // Execute an INSERT query to create a new study record in "לימודים"
                                        int id = -1; // default value in case no result is returned
                                        using (SqlCommand insertCommand = new SqlCommand(INSERT, connection))
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
                                            await insertCommand.ExecuteScalarAsync();
                                            connection.Close();

                                        }
                                        using (SqlCommand selectCommand = new SqlCommand(selectid, connection))
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
                                        if (id != -1)
                                        {
                                            if (studentIDs.Count != 0)
                                            {
                                                foreach (int sid in studentIDs)
                                                {
                                                    for (int option = 8; option <= 10; option++)
                                                    {
                                                        string insertQuery = "INSERT INTO [dbo].[StudentStudies] ([Study], [Option], [Status], [Student], [User], [Date], [Absent]) " +
                                                                              "VALUES (@study, @option, @status, @student, @user, @date, @absent);";
                                                        string selectQuery1 = "SELECT COUNT(*) FROM [dbo].[StudentStudies] WHERE [Study] = @study AND [Option] = @option AND [Status] = @status AND [Student] = @student AND [User] = @user AND [Absent] = @absent";

                                                        using (SqlCommand selectCommand = new SqlCommand(selectQuery1, connection))
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

                                                            if (count1 > 0)
                                                            {
                                                                continue;
                                                            }
                                                            else
                                                            {
                                                                // Row does not exist, proceed with insertion
                                                                using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                                                                {
                                                                    insertCommand.Parameters.AddWithValue("@study", id);
                                                                    insertCommand.Parameters.AddWithValue("@option", option);
                                                                    insertCommand.Parameters.AddWithValue("@status", 1);
                                                                    insertCommand.Parameters.AddWithValue("@student", sid);
                                                                    insertCommand.Parameters.AddWithValue("@user", userID);
                                                                    insertCommand.Parameters.AddWithValue("@date", start);
                                                                    insertCommand.Parameters.AddWithValue("@absent", 0);

                                                                    await connection.OpenAsync();
                                                                    int rowsAffected = await insertCommand.ExecuteNonQueryAsync();
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




                        }
                    }




                }

                // This method asynchronously deletes events that have been marked as deleted from a SQL table
                await DeleteDeletedEventsFromSqlTable(connectionString);



            }
            catch (Exception ex)
            {
                using (StreamWriter writer = File.AppendText("error_log.txt"))
                {
                    // Get the current date and time
                    DateTime now = DateTime.Now;
                    // Format the date and time as a string
                    string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
                    // Write the timestamp and the exception message to the file
                    writer.WriteLine("Error occurred at {0}: {1}", timestamp, ex.Message);
                    // Append a newline after the entry
                    writer.WriteLine();
                }

                // Dispose of the timer object
                ((System.Timers.Timer)sender).Elapsed -= Timer_Elapsed; // Unsubscribe from the event
                ((System.Timers.Timer)sender).Stop();
                ((System.Timers.Timer)sender).Elapsed += Timer_Elapsed;
                // Release the semaphore
                semaphore.Release();
                ((System.Timers.Timer)sender).Start();


            }
        }

      




    }


}
