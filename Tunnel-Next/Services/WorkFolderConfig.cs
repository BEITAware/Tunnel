using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 工作文件夹配置管理器
    /// 支持INI格式配置文件和变量替换
    /// </summary>
    public class WorkFolderConfig
    {
        private const string ConfigFileName = "workfolders.ini";
        private readonly Dictionary<string, string> _config = new();
        private readonly Dictionary<string, string> _variables = new();

        public WorkFolderConfig()
        {
            InitializeVariables();
            LoadConfig();
        }

        /// <summary>
        /// 工作文件夹路径
        /// </summary>
        public string WorkFolder => ExpandVariables(_config.GetValueOrDefault("WorkFolder", "{documents}\\TNX"));

        /// <summary>
        /// 脚本文件夹路径
        /// </summary>
        public string ScriptsFolder => ExpandVariables(_config.GetValueOrDefault("ScriptsFolder", "{documents}\\TNX\\Scripts"));

        /// <summary>
        /// 初始化系统变量
        /// </summary>
        private void InitializeVariables()
        {
            _variables.Clear();
            
            // 添加系统路径变量
            _variables["documents"] = GetSafeDocumentsPath();
            _variables["userprofile"] = GetSafeUserProfilePath();
            _variables["appdata"] = GetSafeAppDataPath();
            _variables["temp"] = Path.GetTempPath();
            
            // 添加应用程序相关变量
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                _variables["appdir"] = appDir;
            }
            catch
            {
                _variables["appdir"] = Environment.CurrentDirectory;
            }
        }

        /// <summary>
        /// 获取安全的用户文档路径
        /// </summary>
        private string GetSafeDocumentsPath()
        {
            try
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrWhiteSpace(documentsPath) && Directory.Exists(documentsPath))
                {
                    return documentsPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 获取用户文档路径失败: {ex.Message}");
            }

            // 回退到用户配置文件路径下的Documents
            try
            {
                var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(userProfilePath) && Directory.Exists(userProfilePath))
                {
                    var documentsInProfile = Path.Combine(userProfilePath, "Documents");
                    if (Directory.Exists(documentsInProfile))
                    {
                        return documentsInProfile;
                    }
                    return userProfilePath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 获取用户配置文件路径失败: {ex.Message}");
            }

            // 最终回退到应用程序数据路径
            return GetSafeAppDataPath();
        }

        /// <summary>
        /// 获取安全的用户配置文件路径
        /// </summary>
        private string GetSafeUserProfilePath()
        {
            try
            {
                var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(userProfilePath) && Directory.Exists(userProfilePath))
                {
                    return userProfilePath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 获取用户配置文件路径失败: {ex.Message}");
            }

            return GetSafeAppDataPath();
        }

        /// <summary>
        /// 获取安全的应用程序数据路径
        /// </summary>
        private string GetSafeAppDataPath()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrWhiteSpace(appDataPath))
                {
                    return appDataPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 获取应用程序数据路径失败: {ex.Message}");
            }

            // 绝对最终回退：使用临时目录
            var tempPath = Path.GetTempPath();
            System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 使用临时目录作为最终回退: {tempPath}");
            return tempPath;
        }

        /// <summary>
        /// 展开配置值中的变量
        /// </summary>
        private string ExpandVariables(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var result = value;
            
            // 使用正则表达式匹配 {变量名} 格式
            var regex = new Regex(@"\{([^}]+)\}", RegexOptions.IgnoreCase);
            var matches = regex.Matches(result);

            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value.ToLowerInvariant();
                if (_variables.TryGetValue(variableName, out var variableValue))
                {
                    result = result.Replace(match.Value, variableValue);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 未知变量: {variableName}");
                }
            }

            return Path.GetFullPath(result);
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFileName))
                {
                    var lines = File.ReadAllLines(ConfigFileName, Encoding.UTF8);
                    ParseIniContent(lines);
                }
                else
                {
                    // 创建默认配置文件
                    CreateDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 加载配置文件失败: {ex.Message}");
                // 使用默认配置
                SetDefaultValues();
            }
        }

        /// <summary>
        /// 解析INI文件内容
        /// </summary>
        private void ParseIniContent(string[] lines)
        {
            _config.Clear();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // 跳过空行和注释行
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                // 解析键值对
                var equalIndex = trimmedLine.IndexOf('=');
                if (equalIndex > 0)
                {
                    var key = trimmedLine.Substring(0, equalIndex).Trim();
                    var value = trimmedLine.Substring(equalIndex + 1).Trim();
                    
                    // 移除值两端的引号
                    if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    
                    _config[key] = value;
                }
            }
        }

        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        private void CreateDefaultConfig()
        {
            SetDefaultValues();
            SaveConfig();
        }

        /// <summary>
        /// 设置默认配置值
        /// </summary>
        private void SetDefaultValues()
        {
            _config["WorkFolder"] = "{documents}\\TNX";
            _config["ScriptsFolder"] = "{documents}\\TNX\\Scripts";
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                var content = new StringBuilder();
                content.AppendLine("; Tunnel-Next 工作文件夹配置");
                content.AppendLine("; 支持的变量: {documents}, {userprofile}, {appdata}, {temp}, {appdir}");
                content.AppendLine();
                content.AppendLine("[Folders]");
                content.AppendLine($"WorkFolder={_config.GetValueOrDefault("WorkFolder", "{documents}\\TNX")}");
                content.AppendLine($"ScriptsFolder={_config.GetValueOrDefault("ScriptsFolder", "{documents}\\TNX\\Scripts")}");
                content.AppendLine();
                content.AppendLine("; 示例:");
                content.AppendLine("; WorkFolder={documents}\\MyTunnelWork");
                content.AppendLine("; ScriptsFolder={documents}\\MyTunnelWork\\Scripts");

                File.WriteAllText(ConfigFileName, content.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderConfig] 保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置工作文件夹路径
        /// </summary>
        public void SetWorkFolder(string path)
        {
            _config["WorkFolder"] = path;
            SaveConfig();
        }

        /// <summary>
        /// 设置脚本文件夹路径
        /// </summary>
        public void SetScriptsFolder(string path)
        {
            _config["ScriptsFolder"] = path;
            SaveConfig();
        }

        /// <summary>
        /// 获取所有可用变量的说明
        /// </summary>
        public Dictionary<string, string> GetAvailableVariables()
        {
            return new Dictionary<string, string>(_variables);
        }
    }
}
