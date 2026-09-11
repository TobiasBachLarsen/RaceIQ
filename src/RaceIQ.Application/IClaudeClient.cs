namespace RaceIQ.Application;

public interface IClaudeClient
{
    Task<string> GenerateAnalysisAsync(string prompt);
}
