using OpenCvSharp;
using Tunnel_Next.Models;
using Tunnel_Next.Services.UI;

using var pixels = new Mat(1, 1, MatType.CV_32FC1, Scalar.All(0.375));
Node Preview(Dictionary<string, object> metadata) => new()
{
    Title = "预览节点",
    IsProcessed = true,
    ProcessedOutputs = new() { ["_metadata"] = metadata, ["f32bmp"] = pixels }
};

var disabled = Preview(new() { ["PreviewEnabled"] = false });
var enabled = Preview(new()
{
    ["AutoScale"] = "false", ["ShowInfo"] = true, ["PreviewTitle"] = "细节"
});
enabled.Parameters.Add(new NodeParameter { Name = "ZoomLevel", Value = "2.5" });
var second = Preview(new());
var connection = new NodeConnection { InputNode = enabled, OutputNode = disabled };
var frame = PreviewNodeFrame.Select([disabled, second, enabled], [connection]);
if (frame == null || !ReferenceEquals(frame.Image, pixels) || frame.Settings.AutoScale ||
    frame.Settings.Zoom != 2.5 || frame.Settings.Caption("1 × 1") != "细节 — 1 × 1")
    throw new Exception("Connected displayable preview or legacy zoom fallback failed.");
if (PreviewNodeFrame.Select([disabled], []) != null || pixels.At<float>(0,0) != .375f)
    throw new Exception("Disabled preview must clear the display and preserve output pixels.");
var defaults = PreviewNodeSettings.Read(null, _ => null);
if (!defaults.Enabled || !defaults.AutoScale || defaults.Zoom != 1 || defaults.ShowInfo || defaults.Caption("1 × 1") != "图像预览")
    throw new Exception("Unexpected defaults for an older graph.");
var invalid = PreviewNodeSettings.Read(new Dictionary<string, object> { ["ZoomLevel"] = "NaN" }, _ => null);
if (invalid.Zoom != 1) throw new Exception("Invalid zoom was not normalized.");
Console.WriteLine("PASS: selection, disabled preview, legacy settings, fit/manual zoom, caption and unchanged pixels.");
