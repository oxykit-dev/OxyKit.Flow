namespace OxyKit.Flow.Services;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    /// <summary>How often the queue worker polls for work.</summary>
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Max jobs claimed per poll.</summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>Default max attempts for new jobs.</summary>
    public int DefaultMaxAttempts { get; set; } = 3;

    /// <summary>Processing jobs older than this are re-queued or dead-lettered.</summary>
    public int ProcessingTimeoutSeconds { get; set; } = 300;
}
