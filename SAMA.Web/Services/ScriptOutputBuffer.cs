using System.Collections.Concurrent;
using SAMA.Web.Models;

namespace SAMA.Web.Services;

public class ScriptOutputBuffer(ILogger<ScriptOutputBuffer> _logger)
{
    private const int MaxEntriesPerCheck = 20;
    private const int MaxOutputLength = 50_000; // 50KB per stream
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<ScriptOutputEntry>> _outputs = new();

    public void Add(Guid checkId, Guid checkResultId, string? stdout, string? stderr)
    {
        // Truncate large outputs to prevent memory bloat
        stdout = TruncateOutput(stdout);
        stderr = TruncateOutput(stderr);

        // Log to Serilog (flows to all configured sinks)
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogInformation(
                "Script stdout for check {CheckId} (result {CheckResultId}): {ScriptOutput}",
                checkId,
                checkResultId,
                stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogWarning(
                "Script stderr for check {CheckId} (result {CheckResultId}): {ScriptError}",
                checkId,
                checkResultId,
                stderr);
        }

        // Only store if there's actual output
        if (string.IsNullOrWhiteSpace(stdout) && string.IsNullOrWhiteSpace(stderr))
        {
            return;
        }

        // Store for UI lookup
        var queue = _outputs.GetOrAdd(checkId, _ => new ConcurrentQueue<ScriptOutputEntry>());
        queue.Enqueue(new ScriptOutputEntry(checkResultId, stdout, stderr, DateTimeOffset.UtcNow));

        // Keep only the most recent entries
        while (queue.Count > MaxEntriesPerCheck)
        {
            queue.TryDequeue(out _);
        }
    }

    private static string? TruncateOutput(string? output)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= MaxOutputLength)
        {
            return output;
        }

        // Keep the end of the output (usually more relevant for debugging)
        var truncatedLength = MaxOutputLength - 50; // Leave room for truncation message
        return $"[...truncated {output.Length - truncatedLength:N0} characters...]\n{output[^truncatedLength..]}";
    }

    public IEnumerable<ScriptOutputEntry> GetOutputs(Guid checkId)
    {
        return _outputs.TryGetValue(checkId, out var queue)
            ? queue.ToArray().Reverse()
            : [];
    }

    public ScriptOutputEntry? GetLatestOutput(Guid checkId)
    {
        return _outputs.TryGetValue(checkId, out var queue)
            ? queue.LastOrDefault()
            : null;
    }
}
