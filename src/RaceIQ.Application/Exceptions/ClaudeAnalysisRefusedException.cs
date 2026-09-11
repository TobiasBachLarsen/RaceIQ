namespace RaceIQ.Application.Exceptions;

public class ClaudeAnalysisRefusedException : Exception
{
    public ClaudeAnalysisRefusedException(string? explanation)
        : base($"Claude declined to generate this analysis. {explanation}") { }
}
