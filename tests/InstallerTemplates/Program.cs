using InstallationWizard;

if (args.Length != 1)
    throw new ArgumentException("请传入已发布的安装向导目录。");

string installer = Path.GetFullPath(args[0]);
string scratch = Directory.CreateTempSubdirectory("Tunnel 模板测试 ").FullName;
try
{
    string work = Path.Combine(scratch, "自选 工作目录");
    string templates = Path.Combine(work, "Resources", "Templates");
    string standard = Path.Combine(templates, "空的节点图");
    string expected = Path.Combine(AppContext.BaseDirectory, "Expected", "空的节点图");
    string graph = Path.Combine(standard, "空的节点图.nodegraph");
    string thumbnail = Path.Combine(standard, "thumbnail.png");
    TemplateInstaller.Install(installer, work);
    AssertSameFile(Path.Combine(expected, "空的节点图.nodegraph"), graph);
    AssertSameFile(Path.Combine(expected, "thumbnail.png"), thumbnail);

    File.WriteAllText(graph, "edited graph");
    string custom = Path.Combine(templates, "自定义模板", "custom.nodegraph");
    Directory.CreateDirectory(Path.GetDirectoryName(custom)!);
    File.WriteAllText(custom, "custom graph");
    File.Delete(thumbnail);
    TemplateInstaller.Install(installer, work);
    if (File.ReadAllText(graph) != "edited graph" || File.ReadAllText(custom) != "custom graph")
        throw new InvalidDataException("重新安装修改了已有模板。");
    AssertSameFile(Path.Combine(expected, "thumbnail.png"), thumbnail);

    string broken = Path.Combine(scratch, "不完整的安装包");
    string incomplete = Path.Combine(broken, "Resources", "Templates", "空的节点图");
    Directory.CreateDirectory(incomplete);
    File.Copy(thumbnail, Path.Combine(incomplete, "thumbnail.png"));
    try
    {
        TemplateInstaller.Install(broken, work);
        throw new InvalidOperationException("缺少节点图时安装应当失败。");
    }
    catch (InvalidDataException) { }
    Console.WriteLine("已验证发布包中的标准模板、重新安装和安装包损坏的处理。");
}
finally
{
    Directory.Delete(scratch, recursive: true);
}

static void AssertSameFile(string expected, string actual)
{
    if (!File.ReadAllBytes(expected).SequenceEqual(File.ReadAllBytes(actual)))
        throw new InvalidDataException($"文件内容与标准模板不符：{actual}");
}
