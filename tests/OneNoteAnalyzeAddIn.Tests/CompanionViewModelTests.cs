using OneNoteAnalyzeAddIn.Ui.ViewModels;

namespace OneNoteAnalyzeAddIn.Tests;

public sealed class CompanionViewModelTests
{
    [Fact]
    public void AppendHistory_KeepsMostRecent20Entries()
    {
        var vm = new CompanionViewModel();
        for (var i = 0; i < 25; i++)
        {
            vm.AppendHistory($"Title {i}", $"Summary {i}");
        }

        Assert.Equal(20, vm.History.Count);
        Assert.Equal("Title 24", vm.History[0].Title);
        Assert.Equal("Title 5", vm.History[^1].Title);
    }
}
