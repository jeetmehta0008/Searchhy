namespace Searchhy;

/// <summary>
/// Centralized configuration for Google Lens visual search.
/// </summary>
public static class LensConfig
{
    /// <summary>
    /// Google Lens main web URL.
    /// </summary>
    public const string LensUrl = "https://lens.google.com/";

    /// <summary>
    /// Google reverse search fallback URL.
    /// </summary>
    public const string FallbackUrl = "https://lens.google.com/";

    /// <summary>
    /// Minimum selection dimensions in pixels to trigger search.
    /// </summary>
    public const int MinSelectionDimensionPx = 15;

    /// <summary>
    /// Required hold duration (ms) for both mouse buttons before chord triggers.
    /// </summary>
    public const int ChordRequiredHoldMs = 100;
}
