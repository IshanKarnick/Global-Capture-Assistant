using OneNoteAnalyzeAddIn.Config;

namespace OneNoteAnalyzeAddIn.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void SetApiKey_ThenGetApiKey_ReturnsOriginalValue()
    {
        var store = new SettingsStore();
        var settings = new AddInSettings();
        const string key = "AIzaSy_TestKey_123";

        store.SetApiKey(settings, key);
        var value = store.GetApiKey(settings);

        Assert.Equal(key, value);
        Assert.NotEqual(key, settings.EncryptedApiKey);
    }
}
