using OneNoteAnalyzeAddIn.Models;

namespace OneNoteAnalyzeAddIn.Capture;

public interface IOverlayCaptureService
{
    Task<CaptureResult> CaptureRegionAsync(CancellationToken cancellationToken);
}
