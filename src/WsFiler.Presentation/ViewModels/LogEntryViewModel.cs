namespace WsFiler.Presentation.ViewModels;

public sealed class LogEntryViewModel(int number, string level, string message)
{
    public int Number { get; } = number;

    public DateTime Timestamp { get; } = DateTime.Now;

    public string Level { get; } = level;

    public string Message { get; } = message;

    public string TimeText => $"{Timestamp:HH:mm:ss}";
}
