using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Models;
using VRCFaceTracking.Services.Logging;

namespace VRCFaceTracking.ViewModels;

/// <summary>
/// One cell of the 102-key /tracking/face/v1 blob, displayed as a heat strip.
/// </summary>
public partial class BlobCell : ObservableObject
{
    private byte _value;

    public int Index { get; }
    public string Key { get; }

    public BlobCell(int index, string key)
    {
        Index = index;
        Key = key;
    }

    public byte Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Brush));
            OnPropertyChanged(nameof(Tooltip));
        }
    }

    public Avalonia.Media.IBrush Brush
    {
        get
        {
            // Quantize to 9 levels to avoid allocating a brush per cell per tick
            var level = Value / 32; // 0..8 (255 -> 7.96 -> 7, so map full range up)
            level = Value >= 250 ? 8 : level;
            return NativeFaceDataDebug.LevelBrushes[level];
        }
    }

    public string Tooltip => $"#{Index} {Key}\n{Value} / 255 = {(Value / 255f):0.00}";
}

public static class NativeFaceDataDebug
{
    // Tracking key order per VRChat's native face tracking OSC spec (/tracking/face/v1)
    public static readonly string[] KeyNames =
    {
        "EyeLookOutRight",   "EyeLookInRight",    "EyeLookUpRight",    "EyeLookDownRight",   // 0-3
        "EyeLookOutLeft",    "EyeLookInLeft",     "EyeLookUpLeft",     "EyeLookDownLeft",    // 4-7
        "EyeClosedRight",    "EyeClosedLeft",     "EyeDilationRight",  "EyeDilationLeft",    // 8-11
        "EyeConstrictRight", "EyeConstrictLeft",                                              // 12-13
        "EyeSquintRight",    "EyeSquintLeft",     "EyeWideRight",      "EyeWideLeft",        // 14-17
        "BrowPinchRight",    "BrowPinchLeft",     "BrowLowererRight",  "BrowLowererLeft",    // 18-21
        "BrowInnerUpRight",  "BrowInnerUpLeft",   "BrowOuterUpRight",  "BrowOuterUpLeft",    // 22-25
        "NasalDilationRight","NasalDilationLeft", "NasalConstrictRight","NasalConstrictLeft",// 26-29
        "CheekSquintRight",  "CheekSquintLeft",   "CheekPuffRight",    "CheekPuffLeft",      // 30-33
        "CheekSuckRight",    "CheekSuckLeft",                                                                // 34-35
        "JawOpen",           "JawRight",          "JawLeft",           "JawForward",         // 36-39
        "JawBackward",       "JawClench",         "JawMandibleRaise",  "MouthClosed",        // 40-43
        "LipSuckUpperRight", "LipSuckUpperLeft",  "LipSuckLowerRight", "LipSuckLowerLeft",   // 44-47
        "LipSuckCornerRight","LipSuckCornerLeft", "LipFunnelUpperRight","LipFunnelUpperLeft",// 48-51
        "LipFunnelLowerRight","LipFunnelLowerLeft","LipPuckerUpperRight","LipPuckerUpperLeft",// 52-55
        "LipPuckerLowerRight","LipPuckerLowerLeft","MouthUpperUpRight", "MouthUpperUpLeft",   // 56-59
        "MouthUpperDeepenRight","MouthUpperDeepenLeft","NoseSneerRight","NoseSneerLeft",         // 60-63
        "MouthLowerDownRight","MouthLowerDownLeft","MouthUpperRight",   "MouthUpperLeft",     // 64-67
        "MouthLowerRight",   "MouthLowerLeft",    "MouthCornerPullRight","MouthCornerPullLeft",// 68-71
        "MouthCornerSlantRight","MouthCornerSlantLeft","MouthFrownRight","MouthFrownLeft",      // 72-75
        "MouthStretchRight", "MouthStretchLeft",  "MouthDimpleRight",  "MouthDimpleLeft",    // 76-79
        "MouthRaiserUpper",  "MouthRaiserLower",  "MouthPressRight",   "MouthPressLeft",     // 80-83
        "MouthTightenerRight","MouthTightenerLeft","TongueOut",         "TongueUp",           // 84-87
        "TongueDown",        "TongueRight",       "TongueLeft",        "TongueRoll",         // 88-91
        "TongueBendDown",    "TongueCurlUp",      "TongueSquish",      "TongueFlat",         // 92-95
        "TongueTwistRight",  "TongueTwistLeft",   "SoftPalateClose",   "ThroatSwallow",      // 96-99
        "NeckFlexRight",     "NeckFlexLeft"                                                          // 100-101
    };

    public static readonly Avalonia.Media.IBrush[] LevelBrushes =
    {
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x3A, 0x3A, 0x44)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x1B, 0x4F, 0x35)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x1F, 0x6B, 0x42)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x24, 0x8A, 0x50)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x2D, 0xA8, 0x5E)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x3F, 0xC1, 0x6C)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x64, 0xD4, 0x8A)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x8F, 0xE3, 0xAD)),
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0xC4, 0xF5, 0xD6)),
    };
}

public partial class OutputViewModel : ObservableRecipient
{
    public ObservableCollection<LogLine> Logs => OutputPageLogger.AllLogs;

    public ObservableCollection<BlobCell> BlobCells { get; } =
        new(Enumerable.Range(0, 102).Select(i => new BlobCell(i, NativeFaceDataDebug.KeyNames[i])));

    [ObservableProperty] private string _eyeSummary = "—";
    [ObservableProperty] private string _nativeStatus = "—";
    [ObservableProperty] private string _diagnosticHint = string.Empty;

    public void UpdateDebugSnapshot()
    {
        var eye = UnifiedTracking.Data.Eye;
        var l = eye.Left.Gaze;
        var r = eye.Right.Gaze;

        EyeSummary =
            $"L gaze ({l.x,6:+0.00;-0.00;0.00}, {l.y,6:+0.00;-0.00;0.00})  open {eye.Left.Openness,4:0.00}      " +
            $"R gaze ({r.x,6:+0.00;-0.00;0.00}, {r.y,6:+0.00;-0.00;0.00})  open {eye.Right.Openness,4:0.00}";

        for (var i = 0; i < BlobCells.Count; i++)
        {
            BlobCells[i].Value = i switch
            {
                0 => ToByte(r.x),
                1 => ToByte(-r.x),
                2 => ToByte(r.y),
                3 => ToByte(-r.y),
                4 => ToByte(-l.x),
                5 => ToByte(l.x),
                6 => ToByte(l.y),
                7 => ToByte(-l.y),
                8 => ToByte(1f - eye.Right.Openness),
                9 => ToByte(1f - eye.Left.Openness),
                >= 10 and <= 13 => (byte)0, // pupil dilation / constriction - not provided by VRCFT
                _ => (byte)(Math.Clamp(UnifiedTracking.Data.Shapes[i - 14].Weight, 0f, 1f) * 255)
            };
        }

        var gazeMagnitude = Math.Max(Math.Abs(l.x) + Math.Abs(l.y), Math.Abs(r.x) + Math.Abs(r.y));
        var gazePresent = gazeMagnitude > 0.01f;

        DiagnosticHint = gazePresent
            ? string.Empty
            : "Gaze floats and the eye-look cells stay at zero: your tracking module is not sending eye position data " +
              "(blinks come from Eye Openness, which is working). Check your module's settings or try a module that provides gaze.";
    }

    private static byte ToByte(float v) => (byte)(Math.Clamp(v, 0f, 1f) * 255);
}
