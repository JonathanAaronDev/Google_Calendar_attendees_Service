
# Google Calendar Attendees Service

## Description
This service is designed to interact with Google Calendar API and Microsoft SQL Server to manage calendar events. It fetches data from the SQL Server database, interacts with Google Calendar to perform desired operations, and logs errors when they occur.

## Features

1. **Modular Architecture**: Separate classes encapsulate Google API calls, SQL operations, and synchronization logic for improved maintainability.
2. **Asynchronous Operations**: The application uses asynchronous programming (async/await) to avoid blocking the main thread, ensuring smooth operations.
3. **Error Handling**: Comprehensive error handling with pluggable notification support.
4. **Timer Based Execution**: The application utilizes a timer to execute specific functions at regular intervals.
5. **Semaphore Synchronization**: The application uses `SemaphoreSlim` for synchronization to ensure that only one operation is being performed at a given time.

## Dependencies

1. **Google Calendar API**: This service interacts with Google Calendar using OAuth2 for authentication and authorization.
2. **Microsoft SQL Server**: This service fetches data from an SQL Server database. Make sure to set up your connection string correctly.

## How to Use

### Setting up the Connection String
The service reads its SQL Server connection string from `App.config` under the name `DbConnection`.

### Error Notifications
Errors encountered during execution are routed through an `IErrorNotifier` implementation. The default notifier writes to the console and can be replaced with custom implementations.

### Running the Service
Run the service, and it will start fetching data from the SQL Server database and interact with Google Calendar based on the logic implemented. Press any key to exit the service.

## Future Improvements

- Replace console-based error notifications with email or messaging alerts.
- Add unit tests and continuous integration workflows.

