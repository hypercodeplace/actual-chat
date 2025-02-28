namespace ActualChat.UI.Blazor.Controls.Internal;

/// <summary> The data transferred from Blazor to JS. </summary>
public class VirtualListRenderState
{
    public long RenderIndex { get; set; }

    public double SpacerSize { get; set; }
    public double EndSpacerSize { get; set; }
    public double? ScrollHeight { get; set; }
    public double? ScrollTop { get; set; }
    public double? ViewportHeight { get; set; }
    public bool HasVeryFirstItem { get; set; }
    public bool HasVeryLastItem { get; set; }

    public string? ScrollToKey { get; set; }
    public bool UseSmoothScroll { get; set; }

    public Dictionary<string, double> ItemSizes { get; set; } = null!;
    public bool HasUnmeasuredItems { get; set; }
    public VirtualListStickyEdgeState? StickyEdge { get; set; }
}
