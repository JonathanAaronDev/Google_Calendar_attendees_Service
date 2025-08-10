
# Google Calendar Attendees Service

## Description
This service is designed to interact with Google Calendar API and Microsoft SQL Server to manage calendar events. It fetches data from the SQL Server database, interacts with Google Calendar to perform desired operations, and logs errors when they occur.

## Features

1. **Modular Architecture**: Separate classes encapsulate Google API calls, SQL operations, and synchronization logic for improved maintainability.
2. **Asynchronous Operations**: The application uses asynchronous programming (async/await) to avoid blocking the main thread, ensuring smooth operations.
3. **Error Handling**: Comprehensive error handling with error logging capabilities.
4. **Timer Based Execution**: The application utilizes a timer to execute specific functions at regular intervals.
5. **Semaphore Synchronization**: The application uses `SemaphoreSlim` for synchronization to ensure that only one operation is being performed at a given time.

## Dependencies

1. **Google Calendar API**: This service interacts with Google Calendar using OAuth2 for authentication and authorization.
2. **Microsoft SQL Server**: This service fetches data from an SQL Server database. Make sure to set up your connection string correctly.

## How to Use

### Setting up the Connection String
The service uses a connection string to connect to the SQL Server database. You can specify your connection string directly in the code or read it from an external file.

### Error Logging
Any errors encountered during the execution are logged to `error_log.txt` with a timestamp.

### Running the Service
Run the service, and it will start fetching data from the SQL Server database and interact with Google Calendar based on the logic implemented. Press any key to exit the service.

## Future Improvements

- Move the connection string to an external configuration file for better security and maintainability.
- Add more comprehensive error handling and possibly send notifications when an error occurs.

