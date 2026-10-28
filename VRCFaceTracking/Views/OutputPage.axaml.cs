using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using VRCFaceTracking.Core.Contracts;
using VRCFaceTracking.Core.Services;
using VRCFaceTracking.Services.Logging;
using VRCFaceTracking.ViewModels;

namespace VRCFaceTracking.Views;

public partial class OutputPage : UserControl
{
    private OutputViewModel ViewModel => (OutputViewModel)DataContext!;
    private const double StickThreshold = 40;
    private bool _snapping;

    private readonly DispatcherTimer _debugTimer;
    private readonly IOscTarget _oscTarget;
    private readonly OscQueryService _oscQueryService;

    public OutputPage()
    {
        InitializeComponent();
        DataContext = Ioc.Default.GetRequiredService<OutputViewModel>();

        LogItems.AddHandler(ScrollViewer.ScrollChangedEvent, OnLogItemsScrollChanged, RoutingStrategies.Bubble);

        _oscTarget = Ioc.Default.GetRequiredService<IOscTarget>();
        _oscQueryService = Ioc.Default.GetRequiredService<OscQueryService>();
        _debugTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _debugTimer.Tick += (_, _) =>
        {
            ViewModel.UpdateDebugSnapshot();
            ViewModel.NativeStatus = _oscTarget.UseNativeFaceTracking
                ? "Native mode: ON (manual) - streaming /tracking/face/v1 to VRChat"
                : _oscQueryService.AvatarInfo.FullFaceTracking
                    ? "Native mode: AUTO - avatar reports full face tracking, streaming /tracking/face/v1 to VRChat"
                    : "Native mode: OFF - sending individual avatar parameters. The preview below shows what native mode would stream.";
        };
        _debugTimer.Start();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _debugTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _debugTimer.Stop();
    }

    private void OnLogItemsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_snapping || LogItems.Scroll is not { } scroll)
            return;

        if (e.ExtentDelta.Y <= 0)
            return;

        var prevExtent = scroll.Extent.Height - e.ExtentDelta.Y;
        var prevTarget = prevExtent - scroll.Viewport.Height;
        var prevOffsetY = scroll.Offset.Y - e.OffsetDelta.Y;
        var wasAtBottom = prevTarget <= 0 || prevTarget - prevOffsetY <= StickThreshold;
        if (!wasAtBottom)
            return;

        var target = scroll.Extent.Height - scroll.Viewport.Height;
        if (target <= 0 || scroll.Offset.Y >= target)
            return;

        _snapping = true;
        try { scroll.Offset = scroll.Offset.WithY(target); }
        finally { _snapping = false; }
    }

    private async void CopyToClipboard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = Ioc.Default.GetRequiredService<LogBufferProvider>().Snapshot();
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
            StatusText.Text = "Copied to clipboard.";
        }
    }

    private async void SaveToFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Log",
            SuggestedFileName = $"vrcft-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            FileTypeChoices = [new FilePickerFileType("Text") { Patterns = ["*.txt"] }]
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            var text = Ioc.Default.GetRequiredService<LogBufferProvider>().Snapshot();
            await writer.WriteAsync(text);
            StatusText.Text = "Log saved.";
        }
    }
}
