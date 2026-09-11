using RaceIQ.Application;

namespace RaceIQ.UnitTests.Fakes;

public class FakeClaudeClient : IClaudeClient
{
    public string ResponseText { get; set; } = "You paced this ride evenly overall.";
    public string? LastPromptReceived { get; private set; }

    public Task<string> GenerateAnalysisAsync(string prompt)
    {
        LastPromptReceived = prompt;
        return Task.FromResult(ResponseText);
    }
}
