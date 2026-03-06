namespace GlobalCaptureAssistant.Ui;

public sealed class NoteCardRenderService : IDisposable
{
    private NoteCardRenderWindow? _window;

    public async Task<byte[]> RenderHtmlToPngAsync(string html, CancellationToken cancellationToken)
    {
        _window ??= new NoteCardRenderWindow();
        return await _window.RenderHtmlToPngAsync(html, cancellationToken).ConfigureAwait(true);
    }

    public void Dispose()
    {
        if (_window is null)
        {
            return;
        }

        _window.Close();
        _window = null;
    }
}
