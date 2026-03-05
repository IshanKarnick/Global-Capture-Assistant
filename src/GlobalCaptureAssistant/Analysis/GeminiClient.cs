using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GlobalCaptureAssistant.Config;
using GlobalCaptureAssistant.Diagnostics;
using GlobalCaptureAssistant.Models;

namespace GlobalCaptureAssistant.Analysis;

public sealed class GeminiClient
{
    private const string PromptSuggestionModelId = "gemma-3-27b-it";

    private static readonly Uri BaseUri = new("https://generativelanguage.googleapis.com/");
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000),
        TimeSpan.FromMilliseconds(2000)
    ];

    private readonly HttpClient _httpClient;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly AppLogger _logger;

    public GeminiClient(SettingsStore settingsStore, AppSettings settings, AppLogger logger)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(Math.Max(10, settings.RequestTimeoutSeconds))
        };
    }

    public async Task<AnalyzeResponse> AnalyzeImageAsync(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        var apiKey = _settingsStore.GetApiKey(_settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured.");
        }

        var modelId = string.IsNullOrWhiteSpace(_settings.ModelId) ? "gemini-3.1-pro-preview" : _settings.ModelId;
        var payload = BuildAnalysisPayload(request);

        Exception? lastException = null;
        var maxRetries = Math.Max(0, _settings.MaxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var message = BuildMessage(modelId, apiKey, payload);
                using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < maxRetries && IsTransient(response.StatusCode))
                    {
                        await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new HttpRequestException($"Gemini request failed with {(int)response.StatusCode}: {TryParseError(body) ?? response.ReasonPhrase}");
                }

                var text = ParseText(body);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Gemini returned an empty response.");
                }

                return new AnalyzeResponse(
                    text,
                    ParseTokenField(body, "promptTokenCount"),
                    ParseTokenField(body, "candidatesTokenCount"),
                    stopwatch.Elapsed,
                    SuggestedPrompts: null);
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException or TaskCanceledException))
            {
                lastException = ex;
                await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        _logger.Error("Gemini image analysis request failed.", lastException);
        throw lastException ?? new InvalidOperationException("Gemini image analysis request failed.");
    }

    public async Task<IReadOnlyList<string>> GenerateSuggestedPromptsAsync(string answer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return BuildFallbackSuggestions(answer);
        }

        var apiKey = _settingsStore.GetApiKey(_settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildFallbackSuggestions(answer);
        }

        var payload = BuildPromptSuggestionPayload(answer);
        Exception? lastException = null;
        var maxRetries = Math.Max(0, _settings.MaxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var message = BuildMessage(PromptSuggestionModelId, apiKey, payload);
                using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < maxRetries && IsTransient(response.StatusCode))
                    {
                        await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    lastException = new HttpRequestException($"Gemma suggestion request failed with {(int)response.StatusCode}: {TryParseError(body) ?? response.ReasonPhrase}");
                    break;
                }

                var suggestions = ParseSuggestedPrompts(body);
                if (suggestions.Count > 0)
                {
                    return suggestions;
                }

                lastException = new InvalidOperationException("Gemma suggestion response was empty.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException or TaskCanceledException))
            {
                lastException = ex;
                await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        _logger.Warn($"Falling back to local suggestions. {lastException?.Message}");
        return BuildFallbackSuggestions(answer);
    }

    private static HttpRequestMessage BuildMessage(string modelId, string apiKey, object payload)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{modelId}:generateContent")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        message.Headers.Add("x-goog-api-key", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return message;
    }

    private static object BuildAnalysisPayload(AnalyzeRequest request)
    {
        var parts = new List<object>
        {
            new
            {
                inlineData = new
                {
                    mimeType = "image/png",
                    data = Convert.ToBase64String(request.ImagePng)
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            parts.Add(new { text = request.UserPrompt.Trim() });
        }

        return new
        {
            contents = new[]
            {
                new
                {
                    parts = parts.ToArray()
                }
            }
        };
    }

    private static object BuildPromptSuggestionPayload(string answer)
    {
        return new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            text =
                                "Based on this assistant answer, generate follow-up prompts the user can click in a sidebar. " +
                                "Return exactly 5 follow-up prompts, one per line. " +
                                "Rules: each prompt under 12 words, actionable, no numbering, no markdown fences.\n\n" +
                                $"Assistant answer:\n{answer}"
                        }
                    }
                }
            }
        };
    }

    private static IReadOnlyList<string> ParseSuggestedPrompts(string body)
    {
        var text = ParseText(body);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var cleaned = StripCodeFence(text.Trim());
        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            JsonElement suggestions;

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                suggestions = doc.RootElement;
            }
            else if (doc.RootElement.TryGetProperty("suggested_prompts", out var explicitPrompts))
            {
                suggestions = explicitPrompts;
            }
            else if (doc.RootElement.TryGetProperty("prompts", out var genericPrompts))
            {
                suggestions = genericPrompts;
            }
            else
            {
                return [];
            }

            if (suggestions.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return suggestions
                .EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct()
                .Take(5)
                .ToList();
        }
        catch
        {
            // Fall back to line-based parsing for models without JSON mode.
            return cleaned
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Select(line => line.TrimStart('-', '*', '•', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ')', ' '))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct()
                .Take(5)
                .ToList();
        }
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return text.Trim('`').Trim();
        }

        var body = text[(firstLineEnd + 1)..];
        var fenceIndex = body.LastIndexOf("```", StringComparison.Ordinal);
        if (fenceIndex >= 0)
        {
            body = body[..fenceIndex];
        }

        return body.Trim();
    }

    private static IReadOnlyList<string> BuildFallbackSuggestions(string answer)
    {
        var hints = (answer ?? string.Empty).ToLowerInvariant();

        var prompts = new List<string>
        {
            "Summarize this in one sentence.",
            "Give me the next 3 actions.",
            "What should I verify first?"
        };

        if (hints.Contains("error") || hints.Contains("code") || hints.Contains("exception"))
        {
            prompts.Add("Suggest a debugging checklist.");
        }
        else if (hints.Contains("email") || hints.Contains("message"))
        {
            prompts.Add("Draft a concise response for me.");
        }
        else
        {
            prompts.Add("Turn this into a task checklist.");
        }

        prompts.Add("What risks should I watch for?");
        return prompts.Distinct().Take(5).ToList();
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or (HttpStatusCode)429
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static string? TryParseError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ParseText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        if (!candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textPart))
            {
                text.AppendLine(textPart.GetString());
            }
        }

        return text.ToString().Trim();
    }

    private static int? ParseTokenField(string body, string field)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("usageMetadata", out var usage) || !usage.TryGetProperty(field, out var tokenValue))
        {
            return null;
        }

        return tokenValue.ValueKind == JsonValueKind.Number ? tokenValue.GetInt32() : null;
    }
}
