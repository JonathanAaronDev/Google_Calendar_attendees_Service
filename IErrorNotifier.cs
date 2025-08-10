using System;
using System.Threading.Tasks;

namespace Google_Calendar_Service
{
    public interface IErrorNotifier
    {
        Task NotifyAsync(Exception ex);
    }
}
