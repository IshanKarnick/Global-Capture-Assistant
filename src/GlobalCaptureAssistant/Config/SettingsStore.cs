using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlobalCaptureAssistant.Config;

public sealed class SettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("GlobalCaptureAssistant::GeminiKey");
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public SettingsStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlobalCaptureAssistant");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _options);
        File.WriteAllText(_settingsPath, json);
    }

    public void SetApiKey(AppSettings settings, string apiKey)
    {
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), Entropy, DataProtectionScope.CurrentUser);
        settings.EncryptedApiKey = Convert.ToBase64String(encrypted);
    }

    public string? GetApiKey(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.EncryptedApiKey))
        {
            return null;
        }

        try
        {
            var encrypted = Convert.FromBase64String(settings.EncryptedApiKey);
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }
}
