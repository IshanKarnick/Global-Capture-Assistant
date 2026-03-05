using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace OneNoteAnalyzeAddIn.Config;

public sealed class SettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("OneNoteAnalyzeAddIn::GeminiKey");
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OneNoteAnalyzeAddIn");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AddInSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AddInSettings();
        }

        var json = File.ReadAllText(_settingsPath);
        return JsonSerializer.Deserialize<AddInSettings>(json) ?? new AddInSettings();
    }

    public void Save(AddInSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public void SetApiKey(AddInSettings settings, string apiKey)
    {
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey), Entropy, DataProtectionScope.CurrentUser);
        settings.EncryptedApiKey = Convert.ToBase64String(encrypted);
    }

    public string? GetApiKey(AddInSettings settings)
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
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
