using System.Windows;
using OneNoteAnalyzeAddIn.Ui.ViewModels;

namespace OneNoteAnalyzeAddIn.Ui;

public partial class CompanionWindow : Window
{
    public CompanionWindow(CompanionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => SnapToRight();
    }

    private void SnapToRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left, workArea.Right - Width - 16);
        Top = workArea.Top + 16;
    }
}
