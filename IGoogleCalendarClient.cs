using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Google_Calendar_Service
{
    public interface IGoogleCalendarClient
    {
        Task<CalendarService> CreateServiceAsync();
        Task<IList<CalendarListEntry>> GetCalendarsAsync(CalendarService service);
        Task<Events> GetEventsAsync(CalendarService service, string calendarId, DateTime timeMin, DateTime timeMax, bool showDeleted = false);
    }
}
