using System.Threading.Tasks;

namespace Google_Calendar_Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var service = new CalendarSyncService();
            await service.RunAsync();
        }
    }
}
