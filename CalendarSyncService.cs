using Google.Apis.Calendar.v3;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Google_Calendar_Service
{
    public class CalendarSyncService
    {
        private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(8);
        private readonly Timer _timer;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly ICalendarRepository _repository;
        private readonly IGoogleCalendarClient _googleClient;
        private readonly IErrorNotifier _notifier;

        public CalendarSyncService() : this(new SqlCalendarRepository(new ConsoleErrorNotifier()), new GoogleCalendarClient(), new ConsoleErrorNotifier())
        {
        }

        public CalendarSyncService(ICalendarRepository repository, IGoogleCalendarClient googleClient, IErrorNotifier notifier)
        {
            _repository = repository;
            _googleClient = googleClient;
            _notifier = notifier;
            _timer = new Timer(SyncInterval.TotalMilliseconds);
            _timer.Elapsed += TimerElapsed;
        }

        public async Task RunAsync()
        {
            await _repository.GetCalendarNamesAsync();
            _timer.Start();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            _timer.Stop();
        }

        private async void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_semaphore.Wait(0))
            {
                return;
            }

            try
            {
                _timer.Stop();
                await SyncCalendarsAsync();
            }
            catch (Exception ex)
            {
                await _notifier.NotifyAsync(ex);
            }
            finally
            {
                _semaphore.Release();
                _timer.Start();
            }
        }

        private async Task SyncCalendarsAsync()
        {
            try
            {
                var service = await _googleClient.CreateServiceAsync();
                var calendars = await _googleClient.GetCalendarsAsync(service);
                await _repository.UpsertCalendarNamesAsync(calendars.Select(c => c.Summary));

                foreach (var calendar in calendars)
                {
                    var events = await _googleClient.GetEventsAsync(service, calendar.Id, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(90));
                    foreach (var eventItem in events.Items)
                    {
                        await _repository.ProcessEventAsync(eventItem, calendar.Summary);
                    }
                }

                await _repository.DeleteDeletedEventsAsync(service);
            }
            catch (Exception ex)
            {
                await _notifier.NotifyAsync(ex);
            }
        }
    }
}
