using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Google_Calendar_Service
{
    public class GoogleCalendarClient : IGoogleCalendarClient
    {
        private const string ApplicationName = "Calendarevents";

        public async Task<CalendarService> CreateServiceAsync()
        {
            UserCredential credential;
            using (var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new[] { CalendarService.Scope.Calendar },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("Google_Calendar_Service"));
            }

            return new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName
            });
        }

        public async Task<IList<CalendarListEntry>> GetCalendarsAsync(CalendarService service)
        {
            CalendarList calendarList = await service.CalendarList.List().ExecuteAsync();
            return calendarList.Items;
        }

        public async Task<Events> GetEventsAsync(CalendarService service, string calendarId, DateTime timeMin, DateTime timeMax, bool showDeleted = false)
        {
            EventsResource.ListRequest request = service.Events.List(calendarId);
            request.ShowDeleted = showDeleted;
            request.TimeMin = timeMin;
            request.TimeMax = timeMax;
            return await request.ExecuteAsync();
        }
    }
}
