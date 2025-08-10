using System;
using System.Threading.Tasks;

namespace Google_Calendar_Service
{
    public class ConsoleErrorNotifier : IErrorNotifier
    {
        public Task NotifyAsync(Exception ex)
        {
            Console.Error.WriteLine(ex);
            return Task.CompletedTask;
        }
    }
}
