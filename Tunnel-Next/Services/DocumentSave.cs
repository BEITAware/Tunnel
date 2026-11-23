using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    internal static class DocumentSave
    {
        public static async Task<bool> SaveModifiedAsync(IEnumerable<IDocumentContent> documents)
        {
            // Saving can show a modal dialog. Work on a snapshot and stop on cancellation.
            foreach (var document in documents.ToArray())
            {
                if (document.IsModified && !await document.SaveAsync())
                    return false;
            }
            return !documents.Any(document => document.IsModified);
        }

        public static async Task WriteGraphAsync(string path, string json)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var temporary = Path.Combine(directory, $".tunnel-{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(temporary, json);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
