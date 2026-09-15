using Anthropic;
using Anthropic.Models.Beta.Messages;
using NonBeta = Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Application.Exceptions;

namespace RaceIQ.Infrastructure.Claude;

public class ClaudeClient : IClaudeClient
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeClient> _logger;

    public ClaudeClient(AnthropicClient client, ILogger<ClaudeClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<string> GenerateAnalysisAsync(string prompt)
    {
        var response = await _client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = "claude-opus-5",
            // Adaptive thinking tokens count toward MaxTokens; keep enough headroom that a
            // full Danish coaching write-up plus thinking doesn't trip the truncation guard.
            MaxTokens = 16000,
            Thinking = new BetaThinkingConfigAdaptive(),
            Betas = ["server-side-fallback-2026-06-01"],
            Fallbacks = new List<BetaFallbackParam> { new(NonBeta.Model.ClaudeOpus4_8) },
            Messages = [new() { Role = Role.User, Content = prompt }],
        });

        if (response.StopReason == "refusal")
        {
            _logger.LogWarning("Claude refused to generate an analysis: {Explanation}", response.StopDetails?.Explanation);
            throw new ClaudeAnalysisRefusedException(response.StopDetails?.Explanation);
        }

        if (response.StopReason == "max_tokens")
        {
            _logger.LogWarning("Claude analysis response was truncated (hit the max_tokens limit)");
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
