namespace SM.Editor.Narrative;

public enum NarrativeDiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public sealed record NarrativeDiagnostic(
    string Code,
    NarrativeDiagnosticSeverity Severity,
    string Message,
    string SourcePath,
    int LineNumber);
