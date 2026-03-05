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
    private readonly GeminiPromptComposer _promptComposer;
    private readonly AppLogger _logger;

    public GeminiClient(SettingsStore settingsStore, AppSettings settings, GeminiPromptComposer promptComposer, AppLogger logger)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _promptComposer = promptComposer;
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
        var payload = BuildPayload(_promptComposer.ComposePrompt(request), request.ImagePng, _settings.ThinkingLevel);
        var maxRetries = Math.Max(0, _settings.MaxRetries);

        Exception? lastException = null;
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

                return new AnalyzeResponse(text, ParseTokenField(body, "promptTokenCount"), ParseTokenField(body, "candidatesTokenCount"), stopwatch.Elapsed);
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

        _logger.Error("Gemini request failed.", lastException);
        throw lastException ?? new InvalidOperationException("Gemini request failed.");
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

    private static object BuildPayload(string prompt, byte[] imagePng, string thinkingLevel)
    {
        return new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inlineData = new
                            {
                                mimeType = "image/png",
                                data = Convert.ToBase64String(imagePng)
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                thinkingConfig = new
                {
                    thinkingLevel = string.IsNullOrWhiteSpace(thinkingLevel) ? "low" : thinkingLevel
                }
            }
        };
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
