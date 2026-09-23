namespace Remnant2UnlockerApp.Models;

public enum DiagnosticStatus
{
    Passed,
    Info,
    Error
}

public sealed class DiagnosticCheckResult
{
    public string Title { get; set; } = "";
    public string Hint { get; set; } = "";
    public string Details { get; set; } = "";
    public string Fix { get; set; } = "";
    public string TechnicalDetails { get; set; } = "";
    public DiagnosticStatus Status { get; set; }

    public bool IsError => Status == DiagnosticStatus.Error;
    public bool IsPassed => Status == DiagnosticStatus.Passed;
}

public sealed class DiagnosticReport
{
    public List<DiagnosticCheckResult> Results { get; set; } = new();

    public int ErrorCount => Results.Count(r => r.Status == DiagnosticStatus.Error);

    public bool HasIssues => ErrorCount > 0;

    public string Hint => HasIssues
        ? Results.First(r => r.Status == DiagnosticStatus.Error).Hint
        : "Setup looks good";

    public string Summary => HasIssues
        ? $"{ErrorCount} of {Results.Count} checks need attention"
        : $"All {Results.Count} checks passed";

    public static DiagnosticReport Empty => new();
}
