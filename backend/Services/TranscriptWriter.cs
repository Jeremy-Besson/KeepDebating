using System.Text.Json;
using TryingStuff.Models;

namespace TryingStuff.Services;

public static class TranscriptWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string WriteToTimestampedJson(DebateTranscript transcript, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"debate-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
        var outputPath = Path.Combine(outputDirectory, fileName);

        var json = JsonSerializer.Serialize(
            transcript,
            JsonOptions);

        File.WriteAllText(outputPath, json);
        return outputPath;
    }
}
