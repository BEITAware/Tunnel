using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// 程序集引用管理器 - 管理脚本编译时的程序集引用
    /// </summary>
    public class AssemblyReferenceManager
    {
        private readonly string _scriptsFolder;
        private readonly string _resourcesFolder;
        private readonly string _globalReferencesConfigPath;
        private readonly Dictionary<string, List<string>> _scriptSpecificReferences = new();
        private readonly List<string> _globalCustomReferences = new();

        public AssemblyReferenceManager(string scriptsFolder, string resourcesFolder)
        {
            _scriptsFolder = scriptsFolder;
            _resourcesFolder = resourcesFolder;
            _globalReferencesConfigPath = Path.Combine(resourcesFolder, "global-references.json");
            LoadGlobalReferences();
        }

        /// <summary>
        /// 获取脚本的所有程序集引用
        /// </summary>
        public List<MetadataReference> GetReferences(string? scriptFilePath = null)
        {
            var references = new List<MetadataReference>();

            try
            {
                // 添加核心引用
                AddCoreReferences(references);

                // 添加WPF引用
                AddWpfReferences(references);

                // 添加第三方库引用
                AddThirdPartyReferences(references);

                // 添加项目引用
                AddProjectReferences(references);

                // 添加System引用
                AddSystemReferences(references);

                // 添加脚本特定引用
                if (!string.IsNullOrEmpty(scriptFilePath))
                {
                    AddScriptSpecificReferences(references, scriptFilePath);
                }

                // 添加全局自定义引用
                AddGlobalCustomReferences(references);

            }
            catch (Exception ex)
            {
            }

            return references;
        }

        /// <summary>
        /// 添加核心.NET引用
        /// </summary>
        private void AddCoreReferences(List<MetadataReference> references)
        {
            try
            {
                // 添加最基础的运行时引用
                var coreAssemblies = new[]
                {
                    typeof(object).Assembly,                    // System.Private.CoreLib
                    typeof(Console).Assembly,                  // System.Console
                    typeof(System.Collections.Generic.List<>).Assembly, // System.Collections
                    typeof(System.Linq.Enumerable).Assembly,  // System.Linq
                    typeof(System.ComponentModel.Component).Assembly, // System.ComponentModel.Primitives
                    typeof(System.Text.Json.JsonSerializer).Assembly, // System.Text.Json
                    typeof(System.IO.File).Assembly,          // System.IO.FileSystem
                    typeof(System.Threading.Tasks.Task).Assembly, // System.Threading.Tasks
                    typeof(System.Reflection.Assembly).Assembly, // System.Reflection
                    typeof(System.Attribute).Assembly         // System.Runtime
                };

                foreach (var assembly in coreAssemblies)
                {
                    TryAddAssemblyReference(references, assembly.Location);
                }

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 添加WPF相关引用
        /// </summary>
        private void AddWpfReferences(List<MetadataReference> references)
        {
            try
            {
                var wpfAssemblies = new[]
                {
                    typeof(System.Windows.FrameworkElement).Assembly,
                    typeof(System.Windows.Controls.Control).Assembly,
                    typeof(System.Windows.UIElement).Assembly,
                    typeof(System.Windows.DependencyObject).Assembly,
                    typeof(System.Windows.Data.Binding).Assembly,
                    typeof(System.Windows.DependencyProperty).Assembly
                };

                foreach (var assembly in wpfAssemblies)
                {
                    TryAddAssemblyReference(references, assembly.Location);
                }

                // 尝试添加System.Xaml.dll
                TryAddSystemXaml(references);

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 尝试添加System.Xaml.dll
        /// </summary>
        private void TryAddSystemXaml(List<MetadataReference> references)
        {
            try
            {
                var bindingAssembly = typeof(System.Windows.Data.Binding).Assembly;
                var windowsDesktopPath = Path.GetDirectoryName(bindingAssembly.Location);

                if (windowsDesktopPath != null)
                {
                    var xamlPath = Path.Combine(windowsDesktopPath, "System.Xaml.dll");
                    if (File.Exists(xamlPath) && TryAddAssemblyReference(references, xamlPath))
                    {
                        return;
                    }
                }

                // 备选方案：从运行时目录查找
                var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (runtimePath != null)
                {
                    var fallbackXamlPath = Path.Combine(runtimePath, "System.Xaml.dll");
                    if (File.Exists(fallbackXamlPath))
                    {
                        TryAddAssemblyReference(references, fallbackXamlPath);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 添加第三方库引用
        /// </summary>
        private void AddThirdPartyReferences(List<MetadataReference> references)
        {
            try
            {
                // 添加OpenCvSharp引用
                TryAddAssemblyReference(references, typeof(OpenCvSharp.Mat).Assembly.Location);

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 添加项目引用
        /// </summary>
        private void AddProjectReferences(List<MetadataReference> references)
        {
            try
            {
                var projectAssemblies = new[]
                {
                    typeof(ITunnelExtensionScript).Assembly,
                    typeof(Microsoft.Win32.OpenFileDialog).Assembly
                };

                foreach (var assembly in projectAssemblies)
                {
                    TryAddAssemblyReference(references, assembly.Location);
                }

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 添加System相关引用
        /// </summary>
        private void AddSystemReferences(List<MetadataReference> references)
        {
            var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (runtimePath == null) return;

            var systemAssemblies = new[]
            {
                // 基础运行时
                "System.Runtime.dll",
                "System.Private.CoreLib.dll",
                "netstandard.dll",

                // 集合和LINQ
                "System.Collections.dll",
                "System.Collections.Concurrent.dll",
                "System.Linq.dll",
                "System.Linq.Expressions.dll",

                // 组件模型
                "System.ComponentModel.dll",
                "System.ComponentModel.Primitives.dll",
                "System.ComponentModel.TypeConverter.dll",
                "System.ObjectModel.dll",

                // IO和文件系统
                "System.IO.dll",
                "System.IO.FileSystem.dll",

                // 线程和任务
                "System.Threading.dll",
                "System.Threading.Tasks.dll",
                "System.Threading.Tasks.Parallel.dll",
                "System.Threading.Thread.dll",
                "System.Threading.ThreadPool.dll",

                // 文本和编码
                "System.Text.Encoding.dll",
                "System.Text.RegularExpressions.dll",
                "System.Text.Json.dll",

                // 数学和转换
                "System.Runtime.Extensions.dll",
                "System.Runtime.Numerics.dll",

                // Win32和互操作
                "Microsoft.Win32.Primitives.dll",
                "System.Runtime.InteropServices.dll",

                // 反射
                "System.Reflection.dll",
                "System.Reflection.Emit.dll",

                // 其他常用
                "System.Console.dll",
                "System.Diagnostics.Debug.dll",
                "System.Globalization.dll"
            };

            int addedCount = 0;
            foreach (var assembly in systemAssemblies)
            {
                var assemblyPath = Path.Combine(runtimePath, assembly);
                if (File.Exists(assemblyPath) && TryAddAssemblyReference(references, assemblyPath))
                {
                    addedCount++;
                }
            }

        }

        /// <summary>
        /// 添加脚本特定引用
        /// </summary>
        private void AddScriptSpecificReferences(List<MetadataReference> references, string scriptFilePath)
        {
            try
            {
                var scriptReferencesPath = GetScriptReferencesPath(scriptFilePath);
                if (File.Exists(scriptReferencesPath))
                {
                    var scriptReferences = LoadScriptReferences(scriptReferencesPath);
                    foreach (var reference in scriptReferences)
                    {
                        TryAddAssemblyReference(references, reference);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 添加全局自定义引用
        /// </summary>
        private void AddGlobalCustomReferences(List<MetadataReference> references)
        {
            try
            {
                foreach (var reference in _globalCustomReferences)
                {
                    TryAddAssemblyReference(references, reference);
                }

                if (_globalCustomReferences.Count > 0)
                {
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 尝试添加程序集引用
        /// </summary>
        private bool TryAddAssemblyReference(List<MetadataReference> references, string assemblyPath)
        {
            try
            {
                if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                {
                    return false;
                }

                // 避免重复添加 - 检查是否已经在当前引用列表中
                if (references.Any(r => string.Equals(r.Display, assemblyPath, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                var metadataRef = MetadataReference.CreateFromFile(assemblyPath);
                references.Add(metadataRef);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取脚本引用配置文件路径
        /// </summary>
        private string GetScriptReferencesPath(string scriptFilePath)
        {
            var scriptName = Path.GetFileNameWithoutExtension(scriptFilePath);
            var scriptResourceFolder = Path.Combine(_resourcesFolder, scriptName);
            return Path.Combine(scriptResourceFolder, "references.json");
        }

        /// <summary>
        /// 加载脚本特定引用
        /// </summary>
        private List<string> LoadScriptReferences(string referencesPath)
        {
            try
            {
                if (!File.Exists(referencesPath))
                    return new List<string>();

                var json = File.ReadAllText(referencesPath);
                var config = JsonSerializer.Deserialize<ScriptReferencesConfig>(json);

                var resolvedReferences = new List<string>();

                if (config?.References != null)
                {
                    foreach (var reference in config.References)
                    {
                        var resolvedPath = ResolveAssemblyPath(reference);
                        if (!string.IsNullOrEmpty(resolvedPath))
                        {
                            resolvedReferences.Add(resolvedPath);
                        }
                    }
                }

                return resolvedReferences;
            }
            catch (Exception ex)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 解析程序集路径
        /// </summary>
        private string? ResolveAssemblyPath(string reference)
        {
            try
            {
                // 如果是绝对路径且文件存在，直接返回
                if (Path.IsPathRooted(reference) && File.Exists(reference))
                    return reference;

                // 如果是相对路径，尝试相对于脚本文件夹解析
                var relativePath = Path.Combine(_scriptsFolder, reference);
                if (File.Exists(relativePath))
                    return relativePath;

                // 尝试从GAC或运行时目录查找
                var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (runtimePath != null)
                {
                    var runtimeAssemblyPath = Path.Combine(runtimePath, reference);
                    if (File.Exists(runtimeAssemblyPath))
                        return runtimeAssemblyPath;
                }

                // 尝试通过Assembly.Load加载已加载的程序集
                try
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(reference);
                    var loadedAssembly = Assembly.Load(assemblyName);
                    if (!string.IsNullOrEmpty(loadedAssembly.Location))
                        return loadedAssembly.Location;
                }
                catch
                {
                    // 忽略加载失败
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 加载全局引用配置
        /// </summary>
        private void LoadGlobalReferences()
        {
            try
            {
                if (!File.Exists(_globalReferencesConfigPath))
                {
                    // 创建默认配置
                    CreateDefaultGlobalReferences();
                    return;
                }

                var json = File.ReadAllText(_globalReferencesConfigPath);
                var config = JsonSerializer.Deserialize<GlobalReferencesConfig>(json);

                if (config?.References != null)
                {
                    _globalCustomReferences.Clear();
                    foreach (var reference in config.References)
                    {
                        var resolvedPath = ResolveAssemblyPath(reference);
                        if (!string.IsNullOrEmpty(resolvedPath))
                        {
                            _globalCustomReferences.Add(resolvedPath);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                CreateDefaultGlobalReferences();
            }
        }

        /// <summary>
        /// 创建默认全局引用配置
        /// </summary>
        private void CreateDefaultGlobalReferences()
        {
            try
            {
                var defaultConfig = new GlobalReferencesConfig
                {
                    References = new List<string>
                    {
                        // 可以在这里添加默认的全局引用
                        // "System.Drawing.dll",
                        // "System.IO.dll"
                    }
                };

                var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                Directory.CreateDirectory(Path.GetDirectoryName(_globalReferencesConfigPath)!);
                File.WriteAllText(_globalReferencesConfigPath, json);

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 添加全局引用
        /// </summary>
        public bool AddGlobalReference(string assemblyPath)
        {
            try
            {
                var resolvedPath = ResolveAssemblyPath(assemblyPath);
                if (string.IsNullOrEmpty(resolvedPath))
                    return false;

                if (!_globalCustomReferences.Contains(resolvedPath))
                {
                    _globalCustomReferences.Add(resolvedPath);
                    SaveGlobalReferences();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 移除全局引用
        /// </summary>
        public bool RemoveGlobalReference(string assemblyPath)
        {
            try
            {
                var resolvedPath = ResolveAssemblyPath(assemblyPath);
                if (string.IsNullOrEmpty(resolvedPath))
                    return false;

                if (_globalCustomReferences.Remove(resolvedPath))
                {
                    SaveGlobalReferences();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 保存全局引用配置
        /// </summary>
        private void SaveGlobalReferences()
        {
            try
            {
                var config = new GlobalReferencesConfig
                {
                    References = new List<string>(_globalCustomReferences)
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_globalReferencesConfigPath, json);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 为脚本创建引用配置文件
        /// </summary>
        public bool CreateScriptReferencesConfig(string scriptFilePath, List<string> references)
        {
            try
            {
                var referencesPath = GetScriptReferencesPath(scriptFilePath);
                var scriptResourceFolder = Path.GetDirectoryName(referencesPath);

                // 确保脚本资源文件夹存在
                if (!string.IsNullOrEmpty(scriptResourceFolder))
                {
                    Directory.CreateDirectory(scriptResourceFolder);
                }

                var config = new ScriptReferencesConfig
                {
                    References = references ?? new List<string>()
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(referencesPath, json);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取所有全局引用
        /// </summary>
        public List<string> GetGlobalReferences()
        {
            return new List<string>(_globalCustomReferences);
        }

        /// <summary>
        /// 获取脚本的资源文件夹路径
        /// </summary>
        public string GetScriptResourceFolder(string scriptFilePath)
        {
            var scriptName = Path.GetFileNameWithoutExtension(scriptFilePath);
            return Path.Combine(_resourcesFolder, scriptName);
        }

        /// <summary>
        /// 确保脚本资源文件夹存在
        /// </summary>
        public bool EnsureScriptResourceFolder(string scriptFilePath)
        {
            try
            {
                var resourceFolder = GetScriptResourceFolder(scriptFilePath);
                Directory.CreateDirectory(resourceFolder);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 全局引用配置
    /// </summary>
    public class GlobalReferencesConfig
    {
        public List<string> References { get; set; } = new();
    }

    /// <summary>
    /// 脚本引用配置
    /// </summary>
    public class ScriptReferencesConfig
    {
        public List<string> References { get; set; } = new();
    }
}
