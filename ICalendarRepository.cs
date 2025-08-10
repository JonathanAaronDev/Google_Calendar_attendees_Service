using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Google_Calendar_Service
{
    public interface ICalendarRepository
    {
        Task<string[]> GetCalendarNamesAsync();
        Task UpsertCalendarNamesAsync(IEnumerable<string> calendarNames);
        Task ProcessEventAsync(Event eventItem, string calendarName);
        Task DeleteDeletedEventsAsync(CalendarService service);
    }
}
