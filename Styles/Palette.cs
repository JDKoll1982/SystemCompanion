using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SystemCompanion.Styles;

/// <summary>
/// Brushes used by view models when they need to colour a value.
/// Created lazily so they exist on the UI thread.
/// </summary>
public static class Palette
{
    public static readonly Color Cyan = Color.FromArgb(255, 0x4C, 0xD7, 0xF0);
    public static readonly Color Violet = Color.FromArgb(255, 0x8B, 0x7C, 0xFF);
    public static readonly Color Mint = Color.FromArgb(255, 0x3D, 0xDC, 0x97);
    public static readonly Color Amber = Color.FromArgb(255, 0xFF, 0xC6, 0x5C);
    public static readonly Color Coral = Color.FromArgb(255, 0xFF, 0x7A, 0x85);
    public static readonly Color Muted = Color.FromArgb(255, 0x9A, 0xA7, 0xB8);

    private static SolidColorBrush? _good;
    private static SolidColorBrush? _warn;
    private static SolidColorBrush? _neutral;
    private static SolidColorBrush? _risk;

    public static SolidColorBrush Good => _good ??= new SolidColorBrush(Mint);
    public static SolidColorBrush Warn => _warn ??= new SolidColorBrush(Amber);
    public static SolidColorBrush Neutral => _neutral ??= new SolidColorBrush(Muted);
    public static SolidColorBrush Risk => _risk ??= new SolidColorBrush(Coral);

    public static SolidColorBrush Tint(Color color, byte alpha = 0x22)
        => new(Color.FromArgb(alpha, color.R, color.G, color.B));

    public static SolidColorBrush ForState(string state) => state switch
    {
        "good" => Good,
        "warn" => Warn,
        _ => Neutral
    };
}
