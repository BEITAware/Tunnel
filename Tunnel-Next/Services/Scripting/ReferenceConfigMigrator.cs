using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// 引用配置文件迁移工具
    /// 将旧的脚本级引用配置文件从脚本文件夹迁移到TunnelExtensionResources文件夹
    /// </summary>
    public class ReferenceConfigMigrator
    {
        private readonly string _scriptsFolder;
        private readonly string _resourcesFolder;

        public ReferenceConfigMigrator(string scriptsFolder, string resourcesFolder)
        {
            _scriptsFolder = scriptsFolder ?? throw new ArgumentNullException(nameof(scriptsFolder));
            _resourcesFolder = resourcesFolder ?? throw new ArgumentNullException(nameof(resourcesFolder));
        }

        /// <summary>
        /// 执行迁移操作
        /// </summary>
        public MigrationResult MigrateReferenceConfigs()
        {
            var result = new MigrationResult();

            try
            {

                // 查找所有旧的引用配置文件
                var oldConfigFiles = FindOldReferenceConfigFiles();

                foreach (var oldConfigFile in oldConfigFiles)
                {
                    try
                    {
                        var migrationItem = MigrateConfigFile(oldConfigFile);
                        result.MigratedFiles.Add(migrationItem);

                        if (migrationItem.Success)
                        {
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailureCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        var failedItem = new MigrationItem
                        {
                            OldPath = oldConfigFile,
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                        result.MigratedFiles.Add(failedItem);
                    }
                }

                // 迁移全局引用配置文件
                MigrateGlobalReferenceConfig(result);

                result.Success = result.FailureCount == 0;

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// 查找所有旧的引用配置文件
        /// </summary>
        private List<string> FindOldReferenceConfigFiles()
        {
            var configFiles = new List<string>();

            try
            {
                // 递归查找所有 *.references.json 文件
                FindConfigFilesRecursive(_scriptsFolder, configFiles);
            }
            catch (Exception ex)
            {
            }

            return configFiles;
        }

        /// <summary>
        /// 递归查找配置文件
        /// </summary>
        private void FindConfigFilesRecursive(string directory, List<string> configFiles)
        {
            try
            {
                // 查找当前目录中的配置文件
                var files = Directory.GetFiles(directory, "*.references.json");
                configFiles.AddRange(files);

                // 递归查找子目录
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    var dirName = Path.GetFileName(subDir);
                    // 跳过编译输出目录和资源目录
                    if (dirName == "compiled" || dirName == "TunnelExtensionResources")
                        continue;

                    FindConfigFilesRecursive(subDir, configFiles);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 迁移单个配置文件
        /// </summary>
        private MigrationItem MigrateConfigFile(string oldConfigPath)
        {
            var item = new MigrationItem { OldPath = oldConfigPath };

            try
            {
                // 解析脚本名称
                var fileName = Path.GetFileNameWithoutExtension(oldConfigPath);
                if (fileName.EndsWith(".references"))
                {
                    var scriptName = fileName.Substring(0, fileName.Length - ".references".Length);

                    // 确定新的路径
                    var scriptResourceFolder = Path.Combine(_resourcesFolder, scriptName);
                    var newConfigPath = Path.Combine(scriptResourceFolder, "references.json");

                    item.NewPath = newConfigPath;
                    item.ScriptName = scriptName;

                    // 创建目标文件夹
                    Directory.CreateDirectory(scriptResourceFolder);

                    // 检查目标文件是否已存在
                    if (File.Exists(newConfigPath))
                    {
                        // 如果目标文件已存在，备份旧文件
                        var backupPath = newConfigPath + ".backup";
                        File.Copy(newConfigPath, backupPath, true);
                    }

                    // 复制文件
                    File.Copy(oldConfigPath, newConfigPath, true);

                    // 验证复制是否成功
                    if (File.Exists(newConfigPath))
                    {
                        // 删除旧文件
                        File.Delete(oldConfigPath);
                        item.Success = true;
                    }
                    else
                    {
                        item.Success = false;
                        item.ErrorMessage = "文件复制失败";
                    }
                }
                else
                {
                    item.Success = false;
                    item.ErrorMessage = "无法解析脚本名称";
                }
            }
            catch (Exception ex)
            {
                item.Success = false;
                item.ErrorMessage = ex.Message;
            }

            return item;
        }

        /// <summary>
        /// 迁移全局引用配置文件
        /// </summary>
        private void MigrateGlobalReferenceConfig(MigrationResult result)
        {
            try
            {
                var oldGlobalConfigPath = Path.Combine(_scriptsFolder, "global-references.json");
                var newGlobalConfigPath = Path.Combine(_resourcesFolder, "global-references.json");

                if (File.Exists(oldGlobalConfigPath))
                {
                    var item = new MigrationItem
                    {
                        OldPath = oldGlobalConfigPath,
                        NewPath = newGlobalConfigPath,
                        ScriptName = "global"
                    };

                    try
                    {
                        // 确保资源文件夹存在
                        Directory.CreateDirectory(_resourcesFolder);

                        // 如果目标文件已存在，备份
                        if (File.Exists(newGlobalConfigPath))
                        {
                            var backupPath = newGlobalConfigPath + ".backup";
                            File.Copy(newGlobalConfigPath, backupPath, true);
                        }

                        // 复制文件
                        File.Copy(oldGlobalConfigPath, newGlobalConfigPath, true);

                        // 验证并删除旧文件
                        if (File.Exists(newGlobalConfigPath))
                        {
                            File.Delete(oldGlobalConfigPath);
                            item.Success = true;
                            result.SuccessCount++;
                        }
                        else
                        {
                            item.Success = false;
                            item.ErrorMessage = "全局配置文件复制失败";
                            result.FailureCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        item.Success = false;
                        item.ErrorMessage = ex.Message;
                        result.FailureCount++;
                    }

                    result.MigratedFiles.Add(item);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 检查是否需要迁移
        /// </summary>
        public bool NeedsMigration()
        {
            try
            {
                // 检查是否存在旧格式的配置文件
                var oldConfigFiles = FindOldReferenceConfigFiles();
                var oldGlobalConfig = Path.Combine(_scriptsFolder, "global-references.json");

                return oldConfigFiles.Count > 0 || File.Exists(oldGlobalConfig);
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 迁移结果
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<MigrationItem> MigratedFiles { get; set; } = new();
    }

    /// <summary>
    /// 迁移项目
    /// </summary>
    public class MigrationItem
    {
        public string OldPath { get; set; } = string.Empty;
        public string? NewPath { get; set; }
        public string? ScriptName { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
