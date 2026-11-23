using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services.UI;

internal sealed record PreviewNodeFrame(Mat Image, PreviewNodeSettings Settings)
{
    public static PreviewNodeFrame? Select(IEnumerable<Node> nodes, IEnumerable<NodeConnection> connections)
    {
        var candidates = nodes.Where(node => node.Title == "预览节点")
            .OrderByDescending(node => node.IsProcessed && connections.Any(c => c.InputNode == node && c.OutputNode != null));

        foreach (var node in candidates)
        {
            if (!node.ProcessedOutputs.TryGetValue("f32bmp", out var output) || output is not Mat image ||
                image.IsDisposed || image.Empty()) continue;
            node.ProcessedOutputs.TryGetValue("_metadata", out var metadata);
            var settings = PreviewNodeSettings.Read(metadata as IReadOnlyDictionary<string, object>,
                key => node.Parameters.FirstOrDefault(parameter => parameter.Name == key)?.Value);
            if (settings.Enabled) return new PreviewNodeFrame(image, settings);
        }
        return null;
    }
}
