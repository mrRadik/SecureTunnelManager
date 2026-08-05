using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace SecureTunnelManager.UI.Styles;

internal static class ThemePalettes
{
    public static IReadOnlyDictionary<string, MediaColor> For(bool isDark) =>
        isDark ? Dark : Light;

    private static readonly IReadOnlyDictionary<string, MediaColor> Dark = new Dictionary<string, MediaColor>(StringComparer.Ordinal)
    {
        ["StmBackgroundBrush"] = ColorFromHex("#1E1E1E"),
        ["StmSurfaceBrush"] = ColorFromHex("#252526"),
        ["StmCardBrush"] = ColorFromHex("#2D2D30"),
        ["StmSidebarBrush"] = ColorFromHex("#252526"),
        ["StmTextPrimaryBrush"] = ColorFromHex("#FFFFFF"),
        ["StmTextSecondaryBrush"] = ColorFromHex("#B8B8B8"),
        ["StmAccentBrush"] = ColorFromHex("#0078D4"),
        ["StmAccentHoverBrush"] = ColorFromHex("#1A86D9"),
        ["StmAccentSubtleBrush"] = ColorFromHex("#1A0078D4"),
        ["StmSuccessBrush"] = ColorFromHex("#107C10"),
        ["StmSuccessHoverBrush"] = ColorFromHex("#0E6B0E"),
        ["StmErrorBrush"] = ColorFromHex("#F1707B"),
        ["StmDestructiveBrush"] = ColorFromHex("#C42B1C"),
        ["StmDestructiveHoverBrush"] = ColorFromHex("#A52618"),
        ["StmBorderBrush"] = ColorFromHex("#3A3A3A"),
        ["StmHoverBrush"] = ColorFromHex("#333337"),
        ["StmSelectedBrush"] = ColorFromHex("#094771"),
        ["StmDisabledTextBrush"] = ColorFromHex("#6D6D6D"),
        ["StmDisabledBackgroundBrush"] = ColorFromHex("#2A2A2A"),
        ["StmWarningBrush"] = ColorFromHex("#CA5010"),
        ["StmScrollbarThumbBrush"] = ColorFromHex("#55FFFFFF"),
        ["StmScrollbarThumbHoverBrush"] = ColorFromHex("#80FFFFFF"),
        ["StmScrollbarThumbPressedBrush"] = ColorFromHex("#0078D4"),
        ["StmTitleBarBrush"] = ColorFromHex("#1E1E1E"),
        ["StmRowZebraBrush"] = ColorFromHex("#14FFFFFF"),
        ["StmRowHoverBrush"] = ColorFromHex("#1FFFFFFF"),
        ["StmRouteConnectorBrush"] = ColorFromHex("#9CA3AF"),
    };

    private static readonly IReadOnlyDictionary<string, MediaColor> Light = new Dictionary<string, MediaColor>(StringComparer.Ordinal)
    {
        ["StmBackgroundBrush"] = ColorFromHex("#F3F3F3"),
        ["StmSurfaceBrush"] = ColorFromHex("#FAFAFA"),
        ["StmCardBrush"] = ColorFromHex("#FFFFFF"),
        ["StmSidebarBrush"] = ColorFromHex("#F9F9F9"),
        ["StmTextPrimaryBrush"] = ColorFromHex("#1F1F1F"),
        ["StmTextSecondaryBrush"] = ColorFromHex("#616161"),
        ["StmAccentBrush"] = ColorFromHex("#0078D4"),
        ["StmAccentHoverBrush"] = ColorFromHex("#106EBE"),
        ["StmAccentSubtleBrush"] = ColorFromHex("#1A0078D4"),
        ["StmSuccessBrush"] = ColorFromHex("#0F7B0F"),
        ["StmSuccessHoverBrush"] = ColorFromHex("#0B5A0B"),
        ["StmErrorBrush"] = ColorFromHex("#C42B1C"),
        ["StmDestructiveBrush"] = ColorFromHex("#C42B1C"),
        ["StmDestructiveHoverBrush"] = ColorFromHex("#A52618"),
        ["StmBorderBrush"] = ColorFromHex("#E0E0E0"),
        ["StmHoverBrush"] = ColorFromHex("#EBEBEB"),
        ["StmSelectedBrush"] = ColorFromHex("#E6F2FA"),
        ["StmDisabledTextBrush"] = ColorFromHex("#9E9E9E"),
        ["StmDisabledBackgroundBrush"] = ColorFromHex("#F5F5F5"),
        ["StmWarningBrush"] = ColorFromHex("#CA5010"),
        ["StmScrollbarThumbBrush"] = ColorFromHex("#40000000"),
        ["StmScrollbarThumbHoverBrush"] = ColorFromHex("#66000000"),
        ["StmScrollbarThumbPressedBrush"] = ColorFromHex("#0078D4"),
        ["StmTitleBarBrush"] = ColorFromHex("#F3F3F3"),
        ["StmRowZebraBrush"] = ColorFromHex("#08000000"),
        ["StmRowHoverBrush"] = ColorFromHex("#0F000000"),
        ["StmRouteConnectorBrush"] = ColorFromHex("#9CA3AF"),
    };

    private static MediaColor ColorFromHex(string hex) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
}
