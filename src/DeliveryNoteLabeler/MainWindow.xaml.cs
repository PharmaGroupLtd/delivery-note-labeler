using System.Windows;
using System.Windows.Input;
using DeliveryNoteLabeler.Core.Services;
using DeliveryNoteLabeler.ViewModels;

namespace DeliveryNoteLabeler;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _dragActive;

    public MainWindow(IReadOnlyList<string> initialPdfs, bool autoProcessQueue)
    {
        InitializeComponent();
        _viewModel = (MainViewModel)DataContext;
        _viewModel.AttachOwner(this);
        _viewModel.Initialize(initialPdfs, autoProcessQueue);
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            _dragActive = true;
            StatusBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
            StatusBorder.Background = (System.Windows.Media.Brush)FindResource("AccentHighlightBrush");
            _viewModel.SetDragStatus();
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (_dragActive)
        {
            EndDragHighlight();
        }

        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        EndDragHighlight();

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files is null)
        {
            return;
        }

        var pdfs = files
            .Where(file => file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(file))
            .ToList();

        if (pdfs.Count == 0)
        {
            _viewModel.SetStatusInvalidDrop();
            e.Handled = true;
            return;
        }

        if (_viewModel.QueueItems.Count == 0)
        {
            _viewModel.LoadDroppedPdfs(pdfs);
        }
        else
        {
            _viewModel.EnqueuePdfs(pdfs);
        }

        e.Handled = true;
    }

    private void EndDragHighlight()
    {
        _dragActive = false;
        StatusBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrushStandard");
        StatusBorder.Background = (System.Windows.Media.Brush)FindResource("StatusBarBackgroundBrush");
        _viewModel.ClearDragStatus();
    }
}
