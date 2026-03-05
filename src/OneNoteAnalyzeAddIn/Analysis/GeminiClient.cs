using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OneNoteAnalyzeAddIn.Config;
using OneNoteAnalyzeAddIn.Diagnostics;
using OneNoteAnalyzeAddIn.Models;

namespace OneNoteAnalyzeAddIn.Analysis;

public sealed class GeminiClient
{
    private static readonly Uri BaseUri = new("https://generativelanguage.googleapis.com/");
    private readonly HttpClient _httpClient;
    private readonly SettingsStore _settingsStore;
    private readonly AddInSettings _settings;
    private readonly GeminiPromptComposer _promptComposer;
    private readonly AppLogger _logger;

    public GeminiClient(SettingsStore settingsStore, AddInSettings settings, GeminiPromptComposer promptComposer, AppLogger logger, HttpClient? httpClient = null)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _promptComposer = promptComposer;
        _logger = logger;

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = BaseUri;
        _httpClient.Timeout = TimeSpan.FromSeconds(45);
    }

    public async Task<AnalyzeResponse> AnalyzeImageAsync(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        var apiKey = _settingsStore.GetApiKey(_settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured.");
        }

        var modelId = string.IsNullOrWhiteSpace(_settings.ModelId) ? "gemini-3.1-pro-preview" : _settings.ModelId;
        var prompt = _promptComposer.ComposePrompt(request);
        var payload = BuildPayload(prompt, request.ImagePng, _settings.ThinkingLevel);
        var json = JsonSerializer.Serialize(payload);

        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{modelId}:generateContent")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        message.Headers.Add("x-goog-api-key", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.Warn($"Gemini request failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body={responseBody}", request.CorrelationId);
            throw new HttpRequestException($"Gemini request failed with {(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        var parsedText = ParseTextResponse(responseBody);
        if (string.IsNullOrWhiteSpace(parsedText))
        {
            throw new InvalidOperationException("Gemini returned an empty response.");
        }

        return new AnalyzeResponse(parsedText, ParseInputTokens(responseBody), ParseOutputTokens(responseBody), stopwatch.Elapsed);
    }

    private static object BuildPayload(string prompt, byte[] imageBytes, string thinkingLevel)
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
                                data = Convert.ToBase64String(imageBytes)
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

    private static string? ParseTextResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textElement))
            {
                text.AppendLine(textElement.GetString());
            }
        }

        return text.ToString().Trim();
    }

    private static int? ParseInputTokens(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return TryReadTokenField(doc, "promptTokenCount");
    }

    private static int? ParseOutputTokens(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return TryReadTokenField(doc, "candidatesTokenCount");
    }

    private static int? TryReadTokenField(JsonDocument doc, string propertyName)
    {
        if (!doc.RootElement.TryGetProperty("usageMetadata", out var usage))
        {
            return null;
        }

        if (!usage.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.GetInt32();
    }
}
