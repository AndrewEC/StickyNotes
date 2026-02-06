namespace StickyNotes.Core.Utils;

using System;
using System.Globalization;
using System.Text;

public sealed class ConsoleLogger<T>
{
    private static readonly CompositeFormat LogMessageFormat
        = CompositeFormat.Parse("[{0}][{1}] => [{2}]");

    private static readonly CompositeFormat ErrorMessageFormat
        = CompositeFormat.Parse("{0} :: {1}");

    private static readonly string DateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public void Log(string message)
    {
        string loggerName = typeof(T).Name;

        string currentTime = DateTime.UtcNow.ToString(DateFormat, CultureInfo.InvariantCulture);

        string logMessage = string.Format(
            CultureInfo.InvariantCulture,
            LogMessageFormat,
            loggerName,
            currentTime,
            message);

        Console.WriteLine(logMessage);
    }

    public void Error(string message, Exception cause) => Log(
        string.Format(
            CultureInfo.InvariantCulture,
            ErrorMessageFormat,
            message,
            cause.StackTrace ?? cause.Message
        )
    );
}