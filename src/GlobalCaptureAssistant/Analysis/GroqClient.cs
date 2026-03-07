using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GlobalCaptureAssistant.Config;
using GlobalCaptureAssistant.Diagnostics;
using GlobalCaptureAssistant.Models;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlobalCaptureAssistant.Analysis;

public sealed class GroqClient
{
    private static readonly Uri BaseUri = new("https://api.groq.com/openai/v1/");
    private const int MaxBase64ImageBytes = 3_000_000;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(800),
        TimeSpan.FromMilliseconds(1500)
    ];

    private readonly HttpClient _httpClient;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly AppLogger _logger;

    public GroqClient(SettingsStore settingsStore, AppSettings settings, AppLogger logger)
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
        var apiKey = _settingsStore.GetGroqApiKey(_settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured.");
        }

        var modelId = string.IsNullOrWhiteSpace(_settings.GroqModelId)
            ? "meta-llama/llama-4-scout-17b-16e-instruct"
            : _settings.GroqModelId;

        var payload = BuildAnalysisPayload(request, modelId);
        Exception? lastException = null;
        var maxRetries = Math.Max(0, _settings.MaxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await SendChatCompletionAsync(payload, apiKey, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < maxRetries && IsTransient(response.StatusCode))
                    {
                        await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new HttpRequestException($"Groq analysis request failed with {(int)response.StatusCode}: {TryParseError(body) ?? response.ReasonPhrase}");
                }

                var text = ParseAssistantText(body);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Groq returned an empty response.");
                }

                return new AnalyzeResponse(
                    text,
                    ParseTokenField(body, "prompt_tokens"),
                    ParseTokenField(body, "completion_tokens"),
                    stopwatch.Elapsed,
                    SuggestedPrompts: null);
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException or TaskCanceledException or JsonException))
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

        _logger.Error("Groq image analysis request failed.", lastException);
        throw lastException ?? new InvalidOperationException("Groq image analysis request failed.");
    }

    public async Task<IReadOnlyList<string>> GenerateSuggestedPromptsAsync(string answer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return BuildFallbackSuggestions(answer);
        }

        var apiKey = _settingsStore.GetGroqApiKey(_settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildFallbackSuggestions(answer);
        }

        var modelId = string.IsNullOrWhiteSpace(_settings.GroqModelId)
            ? "meta-llama/llama-4-scout-17b-16e-instruct"
            : _settings.GroqModelId;
        var payload = BuildPromptSuggestionPayload(answer, modelId);
        Exception? lastException = null;
        var maxRetries = Math.Max(0, _settings.MaxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var response = await SendChatCompletionAsync(payload, apiKey, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < maxRetries && IsTransient(response.StatusCode))
                    {
                        await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    lastException = new HttpRequestException($"Groq suggestion request failed with {(int)response.StatusCode}: {TryParseError(body) ?? response.ReasonPhrase}");
                    break;
                }

                var suggestions = ParseSuggestedPrompts(body);
                if (suggestions.Count > 0)
                {
                    return suggestions;
                }

                lastException = new InvalidOperationException("Groq suggestion response was empty.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException or TaskCanceledException or JsonException))
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

    public async Task<ScreenAnnotationDocument> GenerateAnnotationsAsync(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        var apiKey = _settingsStore.GetGroqApiKey(_settings);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured.");
        }

        var modelId = string.IsNullOrWhiteSpace(_settings.GroqModelId)
            ? "meta-llama/llama-4-scout-17b-16e-instruct"
            : _settings.GroqModelId;

        var payload = BuildPayload(request, modelId);
        Exception? lastException = null;
        var maxRetries = Math.Max(0, _settings.MaxRetries);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var response = await SendChatCompletionAsync(payload, apiKey, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < maxRetries && IsTransient(response.StatusCode))
                    {
                        await Task.Delay(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new HttpRequestException($"Groq annotation request failed with {(int)response.StatusCode}: {TryParseError(body) ?? response.ReasonPhrase}");
                }

                return ParseAnnotationDocument(body);
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException or TaskCanceledException or JsonException))
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

        _logger.Error("Groq annotation request failed.", lastException);
        throw lastException ?? new InvalidOperationException("Groq annotation request failed.");
    }

    private static object BuildPayload(AnalyzeRequest request, string modelId)
    {
        var encodedImage = NormalizeVisionImage(request.ImagePng);
        var systemPrompt =
            "You annotate screenshots for an educational desktop overlay. " +
            "Return JSON only with a top-level object {\"annotations\": [...]}. " +
            "Use relative coordinates from 0.0 to 1.0. " +
            "Allowed types: highlight_box, arrow, label, equation, note_panel, solution_panel, explanation_panel. " +
            "Use x and y as the on-image anchor the arrow should point to. " +
            "For highlight_box use x,y,width,height,text,color,emphasis. " +
            "For arrow use x,y,endX,endY,text,color. Width and height can be 0. " +
            "For label use x,y,width,height,title,text,color,emphasis. " +
            "For equation use x,y,width,height,title,latex,text,color. " +
            "For note_panel, solution_panel, and explanation_panel use x,y,width,height,title,text,color,emphasis. " +
            "Use markdown in text for bullets, numbered steps, short notes, and worked solutions. " +
            "Use inline or block LaTeX in text when math helps. " +
            "If the screenshot contains an exercise, question, or problem, you may solve it and place the worked answer in a solution_panel. " +
            "If the screenshot is dense or conceptual, add note_panel or explanation_panel content that teaches the user what matters. " +
            "The UI will place content boxes on the left or right side outside the screenshot, so focus on accurate anchor points and concise side callouts. " +
            "Prefer 1 to 3 larger, readable teaching panels over many tiny labels. " +
            "Use the screenshot area mostly for arrows and optional highlight boxes, not large text blocks. " +
            "Prefer richer teaching panels when the screenshot contains exercises, diagrams, or concepts that need explanation. " +
            "Keep text concise and useful. Mix explanation, tutoring, and debugging where relevant. " +
            "No markdown, no prose outside JSON.";

        var userText = new StringBuilder();
        userText.AppendLine("Annotate the whole screenshot.");
        if (request.WindowContext is not null)
        {
            userText.AppendLine($"Process: {request.WindowContext.ProcessName}");
            userText.AppendLine($"Window title: {request.WindowContext.Title}");
        }

        return new
        {
            model = modelId,
            temperature = 0.2,
            response_format = new
            {
                type = "json_object"
            },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = userText.ToString().Trim()
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{encodedImage.MediaType};base64,{Convert.ToBase64String(encodedImage.Bytes)}"
                            }
                        }
                    }
                }
            }
        };
    }

    private static object BuildAnalysisPayload(AnalyzeRequest request, string modelId)
    {
        var encodedImage = NormalizeVisionImage(request.ImagePng);
        var prompt = new StringBuilder();
        prompt.AppendLine(string.IsNullOrWhiteSpace(request.UserPrompt)
            ? "Explain what is shown in this screenshot and focus on the most important details."
            : request.UserPrompt.Trim());

        if (request.WindowContext is not null)
        {
            prompt.AppendLine();
            prompt.AppendLine("Window context:");
            prompt.AppendLine($"- Process: {request.WindowContext.ProcessName}");
            prompt.AppendLine($"- Title: {request.WindowContext.Title}");
        }

        return new
        {
            model = modelId,
            temperature = 0.3,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are a desktop screenshot assistant. Answer clearly and concisely, and use markdown when it improves readability."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = prompt.ToString().Trim()
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:{encodedImage.MediaType};base64,{Convert.ToBase64String(encodedImage.Bytes)}"
                            }
                        }
                    }
                }
            }
        };
    }

    private static object BuildPromptSuggestionPayload(string answer, string modelId)
    {
        return new
        {
            model = modelId,
            temperature = 0.2,
            response_format = new
            {
                type = "json_object"
            },
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content =
                        "Based on this assistant answer, generate follow-up prompts the user can click in a sidebar. " +
                        "Return JSON only in the shape {\"prompts\":[...]} with exactly 5 prompts. " +
                        "Rules: each prompt under 12 words, actionable, no numbering.\n\n" +
                        $"Assistant answer:\n{answer}"
                }
            }
        };
    }

    private static ScreenAnnotationDocument ParseAnnotationDocument(string body)
    {
        using var responseDoc = JsonDocument.Parse(body);
        var contentElement = responseDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content");
        var content = ReadContentPayload(contentElement);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Groq returned an empty annotation payload.");
        }

        return ScreenAnnotationParser.Parse(content, "Groq");
    }

    private static string? ReadContentPayload(JsonElement contentElement)
    {
        return contentElement.ValueKind switch
        {
            JsonValueKind.String => contentElement.GetString(),
            JsonValueKind.Array => ReadContentParts(contentElement),
            JsonValueKind.Object => contentElement.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => contentElement.GetRawText(),
            _ => null
        };
    }

    private static string? ReadContentParts(JsonElement contentParts)
    {
        var text = new StringBuilder();

        foreach (var part in contentParts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                text.Append(part.GetString());
                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (part.TryGetProperty("text", out var textElement))
            {
                text.Append(ReadFlexibleValue(textElement));
            }
            else if (part.TryGetProperty("input", out var inputElement))
            {
                text.Append(ReadFlexibleValue(inputElement));
            }
        }

        return text.Length == 0 ? null : text.ToString();
    }

    private static string? ReadFlexibleValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => element.GetRawText(),
            JsonValueKind.Array => element.GetRawText(),
            _ => null
        };
    }

    private async Task<HttpResponseMessage> SendChatCompletionAsync(object payload, string apiKey, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private static string? ParseAssistantText(string body)
    {
        using var responseDoc = JsonDocument.Parse(body);
        var contentElement = responseDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content");
        return ReadContentPayload(contentElement)?.Trim();
    }

    private static IReadOnlyList<string> ParseSuggestedPrompts(string body)
    {
        var text = ParseAssistantText(body);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("prompts", out var prompts) || prompts.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return prompts.EnumerateArray()
                .Select(item => ReadFlexibleValue(item))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .Distinct()
                .Take(5)
                .ToList();
        }
        catch
        {
            return text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct()
                .Take(5)
                .ToList();
        }
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
                ? ReadFlexibleValue(message)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseTokenField(string body, string propertyName)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("usage", out var usage) || !usage.TryGetProperty(propertyName, out var tokenValue))
        {
            return null;
        }

        return tokenValue.ValueKind == JsonValueKind.Number ? tokenValue.GetInt32() : null;
    }

    private static VisionImagePayload NormalizeVisionImage(byte[] sourcePngBytes)
    {
        if (sourcePngBytes.Length <= MaxBase64ImageBytes)
        {
            return new VisionImagePayload(sourcePngBytes, "image/png");
        }

        using var input = new MemoryStream(sourcePngBytes);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();

        BitmapSource workingFrame = frame;
        var scale = 1.0;
        var jpegQualities = new[] { 92, 84, 76, 68 };

        foreach (var quality in jpegQualities)
        {
            var jpegBytes = EncodeJpeg(workingFrame, quality);
            if (jpegBytes.Length <= MaxBase64ImageBytes)
            {
                return new VisionImagePayload(jpegBytes, "image/jpeg");
            }
        }

        while (scale > 0.4)
        {
            scale *= 0.85;
            workingFrame = ResizeFrame(frame, scale);

            foreach (var quality in jpegQualities)
            {
                var jpegBytes = EncodeJpeg(workingFrame, quality);
                if (jpegBytes.Length <= MaxBase64ImageBytes)
                {
                    return new VisionImagePayload(jpegBytes, "image/jpeg");
                }
            }
        }

        throw new InvalidOperationException("Captured image is too large for Groq vision even after downscaling.");
    }

    private static byte[] EncodeJpeg(BitmapSource source, int qualityLevel)
    {
        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = qualityLevel
        };
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static BitmapSource ResizeFrame(BitmapSource source, double scale)
    {
        var transform = new ScaleTransform(scale, scale);
        var resized = new TransformedBitmap(source, transform);
        resized.Freeze();
        return resized;
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

    private sealed record VisionImagePayload(byte[] Bytes, string MediaType);
}
