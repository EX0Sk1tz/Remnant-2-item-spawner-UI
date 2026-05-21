namespace Remnant2UnlockerApp.Models;

public sealed class DiagnosticIssue
{
    public string Title { get; set; } = "";
    public string Hint { get; set; } = "";
    public string Details { get; set; } = "";
    public string Fix { get; set; } = "";
    public string TechnicalDetails { get; set; } = "";
    public bool IsError { get; set; }
}

public sealed class DiagnosticReport
{
    public List<DiagnosticIssue> Issues { get; set; } = new();

    public bool HasIssues => Issues.Count > 0;

    public string Hint => HasIssues
        ? Issues[0].Hint
        : "Setup looks good";

    public string Summary => HasIssues
        ? $"{Issues.Count} setup issue(s) found"
        : "No setup issues found";

    public static DiagnosticReport Empty => new();
}