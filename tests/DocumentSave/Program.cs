using Tunnel_Next.Models;
using Tunnel_Next.Services;

var kept = new Document(false);
var saved = new Document(true);
var cancelled = new Document(true) { AllowSave = false };
var pending = new Document(true);
var documents = new[] { kept, saved, cancelled, pending };
if (await DocumentSave.SaveModifiedAsync(documents) || kept.Saves != 0 || saved.Saves != 1 ||
    !cancelled.IsModified || pending.Saves != 0)
    throw new Exception("Closing must stop at a failed or cancelled save.");
cancelled.AllowSave = true;
if (!await DocumentSave.SaveModifiedAsync(documents) || saved.Saves != 1 || pending.Saves != 1)
    throw new Exception("Retry must save only outstanding changes.");
saved.IsModified = true;
saved.EditedWhileSaving = true;
if (await DocumentSave.SaveModifiedAsync(documents))
    throw new Exception("A graph edited while saving must stay open.");

var root = Directory.CreateTempSubdirectory();
try
{
    var graph = Path.Combine(root.FullName, "项目.nodegraph");
    await DocumentSave.WriteGraphAsync(graph, "first");
    await DocumentSave.WriteGraphAsync(graph, "second");
    if (await File.ReadAllTextAsync(graph) != "second")
        throw new Exception("The saved graph was not replaced.");
    var blocked = Directory.CreateDirectory(Path.Combine(root.FullName, "blocked.nodegraph"));
    try
    {
        await DocumentSave.WriteGraphAsync(blocked.FullName, "new");
        throw new Exception("Invalid destination unexpectedly accepted");
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
    if (Directory.EnumerateFiles(root.FullName, "*.tmp").Any() || !Directory.Exists(blocked.FullName))
        throw new Exception("Failed save changed the destination or leaked temporary files.");
    Console.WriteLine("PASS: cancellation, retry, concurrent edits, replacement and failure cleanup");
}
finally { root.Delete(true); }

sealed class Document(bool modified) : IDocumentContent
{
    public bool IsModified { get; set; } = modified;
    public int Saves { get; private set; }
    public bool AllowSave { get; set; } = true;
    public bool EditedWhileSaving { get; set; }
    public Task<bool> SaveAsync(string? filePath = null)
    {
        Saves++;
        if (AllowSave) IsModified = EditedWhileSaving;
        return Task.FromResult(AllowSave);
    }
    public DocumentType DocumentType => DocumentType.NodeGraph;
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "test";
    public string? FilePath { get; set; }
    public bool CanClose { get; set; }
    public event EventHandler<EventArgs>? TitleChanged { add { } remove { } }
    public event EventHandler<EventArgs>? ModifiedChanged { add { } remove { } }
    public event EventHandler<EventArgs>? CloseRequested { add { } remove { } }
    public System.Windows.FrameworkElement GetContentControl() => throw new NotSupportedException();
    public bool Save(string? filePath = null) => throw new NotSupportedException();
    public bool CheckCanClose() => true;
    public void OnActivated() { }
    public void OnDeactivated() { }
    public void Dispose() { }
}

#if !WINDOWS
// The document interface references this WPF type. These tests do not create UI controls.
namespace System.Windows { public class FrameworkElement { } }
#endif
