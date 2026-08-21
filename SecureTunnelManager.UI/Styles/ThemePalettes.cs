using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace SecureTunnelManager.UI.Styles;

internal static class ThemePalettes
{
    public static IReadOnlyDictionary<string, MediaColor> For(bool isDark) =>
        isDark ? Dark : Light;

    private static readonly IReadOnlyDictionary<string, MediaColor> Dark = new Dictionary<string, MediaColor>(StringComparer.Ordinal)
    {
        ["StmBackgroundBrush"] = ColorFromHex("#1C1E24"),
        ["StmSurfaceBrush"] = ColorFromHex("#23262E"),
        ["StmCardBrush"] = ColorFromHex("#2A2D38"),
        ["StmCardHoverBrush"] = ColorFromHex("#30343E"),
        ["StmSidebarBrush"] = ColorFromHex("#23262E"),
        ["StmTextPrimaryBrush"] = ColorFromHex("#FFFFFF"),
        ["StmTextSecondaryBrush"] = ColorFromHex("#B0B4C0"),
        ["StmAccentBrush"] = ColorFromHex("#0078D4"),
        ["StmAccentHoverBrush"] = ColorFromHex("#1A86D9"),
        ["StmAccentSubtleBrush"] = ColorFromHex("#1A0078D4"),
        ["StmSuccessBrush"] = ColorFromHex("#107C10"),
        ["StmSuccessHoverBrush"] = ColorFromHex("#0E6B0E"),
        ["StmErrorBrush"] = ColorFromHex("#F1707B"),
        ["StmDestructiveBrush"] = ColorFromHex("#C42B1C"),
        ["StmDestructiveHoverBrush"] = ColorFromHex("#A52618"),
        ["StmBorderBrush"] = ColorFromHex("#383C48"),
        ["StmHoverBrush"] = ColorFromHex("#30343E"),
        ["StmSelectedBrush"] = ColorFromHex("#0C3A5E"),
        ["StmDisabledTextBrush"] = ColorFromHex("#6A7080"),
        ["StmDisabledBackgroundBrush"] = ColorFromHex("#282C34"),
        ["StmWarningBrush"] = ColorFromHex("#CA5010"),
        ["StmScrollbarThumbBrush"] = ColorFromHex("#55FFFFFF"),
        ["StmScrollbarThumbHoverBrush"] = ColorFromHex("#80FFFFFF"),
        ["StmScrollbarThumbPressedBrush"] = ColorFromHex("#0078D4"),
        ["StmTitleBarBrush"] = ColorFromHex("#1C1E24"),
        ["StmRowZebraBrush"] = ColorFromHex("#14FFFFFF"),
        ["StmRowHoverBrush"] = ColorFromHex("#1FFFFFFF"),
        ["StmRouteConnectorBrush"] = ColorFromHex("#8B95A8"),
    };

    private static readonly IReadOnlyDictionary<string, MediaColor> Light = new Dictionary<string, MediaColor>(StringComparer.Ordinal)
    {
        ["StmBackgroundBrush"] = ColorFromHex("#EFF1F5"),
        ["StmSurfaceBrush"] = ColorFromHex("#F6F7FA"),
        ["StmCardBrush"] = ColorFromHex("#FFFFFF"),
        ["StmCardHoverBrush"] = ColorFromHex("#F3F5F9"),
        ["StmSidebarBrush"] = ColorFromHex("#F4F5F9"),
        ["StmTextPrimaryBrush"] = ColorFromHex("#1F1F1F"),
        ["StmTextSecondaryBrush"] = ColorFromHex("#5C6472"),
        ["StmAccentBrush"] = ColorFromHex("#0078D4"),
        ["StmAccentHoverBrush"] = ColorFromHex("#106EBE"),
        ["StmAccentSubtleBrush"] = ColorFromHex("#1A0078D4"),
        ["StmSuccessBrush"] = ColorFromHex("#0F7B0F"),
        ["StmSuccessHoverBrush"] = ColorFromHex("#0B5A0B"),
        ["StmErrorBrush"] = ColorFromHex("#C42B1C"),
        ["StmDestructiveBrush"] = ColorFromHex("#C42B1C"),
        ["StmDestructiveHoverBrush"] = ColorFromHex("#A52618"),
        ["StmBorderBrush"] = ColorFromHex("#D8DCE5"),
        ["StmHoverBrush"] = ColorFromHex("#E5E8EF"),
        ["StmSelectedBrush"] = ColorFromHex("#DCE8F5"),
        ["StmDisabledTextBrush"] = ColorFromHex("#9AA0AD"),
        ["StmDisabledBackgroundBrush"] = ColorFromHex("#EFF2F7"),
        ["StmWarningBrush"] = ColorFromHex("#CA5010"),
        ["StmScrollbarThumbBrush"] = ColorFromHex("#40000000"),
        ["StmScrollbarThumbHoverBrush"] = ColorFromHex("#66000000"),
        ["StmScrollbarThumbPressedBrush"] = ColorFromHex("#0078D4"),
        ["StmTitleBarBrush"] = ColorFromHex("#EFF1F5"),
        ["StmRowZebraBrush"] = ColorFromHex("#08000000"),
        ["StmRowHoverBrush"] = ColorFromHex("#0F000000"),
        ["StmRouteConnectorBrush"] = ColorFromHex("#8B95A8"),
    };

    private static MediaColor ColorFromHex(string hex) =>
        (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
}
