using System;
using System.IO;
using System.Linq;

namespace InstallationWizard
{
    internal static class TemplateInstaller
    {
        public static void Install(string installerFolder, string workFolder)
        {
            string source = Path.Combine(installerFolder, "Resources", "Templates");
            string destination = Path.Combine(workFolder, "Resources", "Templates");
            var templateFolders = Directory.GetDirectories(source);
            if (templateFolders.Length == 0)
                throw new InvalidDataException($"安装包中缺少标准模板：{source}");

            foreach (string folder in templateFolders)
            {
                if (!Directory.EnumerateFiles(folder, "*.nodegraph").Any())
                    throw new InvalidDataException($"标准模板中缺少节点图文件：{folder}");

                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(destination, Path.GetRelativePath(source, file));
                    InstallMissingFile(file, target);
                }
            }
        }

        private static void InstallMissingFile(string source, string target)
        {
            if (File.Exists(target)) return;

            string parent = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(parent);
            // 先复制到同一目录的临时文件，避免安装中断后把不完整文件当作已有模板。
            string staged = Path.Combine(parent, Path.GetRandomFileName());
            try
            {
                File.Copy(source, staged);
                File.Move(staged, target);
            }
            catch (IOException) when (File.Exists(target))
            {
                // 另一个安装进程已补齐该文件。
            }
            finally
            {
                File.Delete(staged);
            }
        }
    }
}
