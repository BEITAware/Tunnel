using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tunnel_Next.Services.UI;

internal sealed record PreviewNodeSettings(bool Enabled, bool AutoScale, double Zoom, string Title, bool ShowInfo)
{
    public static PreviewNodeSettings Read(IReadOnlyDictionary<string, object>? metadata, Func<string, object?> parameter)
    {
        object? ReadValue(string key, string? parameterKey = null) =>
            metadata != null && metadata.TryGetValue(key, out var value) ? value : parameter(parameterKey ?? key);

        bool Flag(string key, bool fallback, string? parameterKey = null) =>
            bool.TryParse(ReadValue(key, parameterKey)?.ToString(), out var flag) ? flag : fallback;

        var rawZoom = Convert.ToString(ReadValue("ZoomLevel"), CultureInfo.InvariantCulture);
        if (!double.TryParse(rawZoom, NumberStyles.Float, CultureInfo.InvariantCulture, out double zoom) ||
            !double.IsFinite(zoom) || zoom <= 0)
            zoom = 1;

        return new PreviewNodeSettings(Flag("PreviewEnabled", true, "EnablePreview"),
            Flag("AutoScale", true), zoom, ReadValue("PreviewTitle")?.ToString() ?? "图像预览", Flag("ShowInfo", false));
    }

    public string Caption(string imageInfo) =>
        !ShowInfo ? Title : string.IsNullOrEmpty(Title) ? imageInfo : $"{Title} — {imageInfo}";
}
