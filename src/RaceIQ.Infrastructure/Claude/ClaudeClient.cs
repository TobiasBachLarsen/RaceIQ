using Anthropic;
using Anthropic.Models.Beta.Messages;
using NonBeta = Anthropic.Models.Messages;
using RaceIQ.Application;
using RaceIQ.Application.Exceptions;

namespace RaceIQ.Infrastructure.Claude;

public class ClaudeClient : IClaudeClient
{
    private readonly AnthropicClient _client;

    public ClaudeClient(AnthropicClient client)
    {
        _client = client;
    }

    public async Task<string> GenerateAnalysisAsync(string prompt)
    {
        var response = await _client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = "claude-opus-5",
            MaxTokens = 4096,
            Thinking = new BetaThinkingConfigAdaptive(),
            Betas = ["server-side-fallback-2026-06-01"],
            Fallbacks = new List<BetaFallbackParam> { new(NonBeta.Model.ClaudeOpus4_8) },
            Messages = [new() { Role = Role.User, Content = prompt }],
        });

        if (response.StopReason == "refusal")
        {
            throw new ClaudeAnalysisRefusedException(response.StopDetails?.Explanation);
        }

        if (response.StopReason == "max_tokens")
        {
            throw new InvalidOperationException(
                "Claude's response was truncated before completing the analysis (hit the max_tokens limit).");
        }

        var textBlocks = response.Content
            .Select(b => b.Value)
            .OfType<BetaTextBlock>()
            .Select(b => b.Text);

        return string.Join("\n", textBlocks);
    }
}
