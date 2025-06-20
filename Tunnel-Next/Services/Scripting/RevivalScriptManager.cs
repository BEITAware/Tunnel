using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// Revival Scripts管理器 - 管理用户脚本系统
    /// </summary>
    public class RevivalScriptManager
    {
        private readonly string _userScriptsFolder;
        private readonly string _userResourcesFolder;
        private readonly string _compiledFolder;
        private readonly string _compilationCacheFile;
        private readonly ConcurrentDictionary<string, RevivalScriptInfo> _scriptRegistry = new();
        private readonly AssemblyReferenceManager _assemblyReferenceManager;
        private CompilationCache _compilationCache = new();
        // 移除了_compiledScripts字段，不再缓存实例
        private volatile bool _isInitialized = false;
        private readonly object _compilationCacheLock = new object();

        /// <summary>
        /// 脚本系统是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 用户脚本文件夹路径
        /// </summary>
        public string UserScriptsFolder => _userScriptsFolder;

        /// <summary>
        /// 用户资源文件夹路径
        /// </summary>
        public string UserResourcesFolder => _userResourcesFolder;

        /// <summary>
        /// 脚本编译完成事件
        /// </summary>
        public event EventHandler? ScriptsCompilationCompleted;

        public RevivalScriptManager(string userScriptsFolder, string userResourcesFolder)
        {
            _userScriptsFolder = userScriptsFolder ?? throw new ArgumentNullException(nameof(userScriptsFolder));
            _userResourcesFolder = userResourcesFolder ?? throw new ArgumentNullException(nameof(userResourcesFolder));
            _compiledFolder = Path.Combine(_userScriptsFolder, "compiled");
            _compilationCacheFile = Path.Combine(_compiledFolder, "compilation-cache.json");
            _assemblyReferenceManager = new AssemblyReferenceManager(_userScriptsFolder, _userResourcesFolder);

            // 确保文件夹存在
            Directory.CreateDirectory(_userScriptsFolder);
            Directory.CreateDirectory(_userResourcesFolder);
            Directory.CreateDirectory(_compiledFolder);

            // 加载编译缓存
            LoadCompilationCache();

            // 清理无效的编译文件
            CleanupInvalidCompiledFiles();
        }

        /// <summary>
        /// 扫描Revival Scripts
        /// </summary>
        public void ScanRevivalScripts()
        {
            // 检查并执行引用配置文件迁移
            MigrateReferenceConfigsIfNeeded();

            _scriptRegistry.Clear();

            // 递归扫描所有.cs文件
            ScanDirectory(_userScriptsFolder);
        }

        /// <summary>
        /// 异步扫描Revival Scripts（多线程版本）
        /// </summary>
        public async Task ScanRevivalScriptsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report("正在迁移引用配置文件...");

            // 检查并执行引用配置文件迁移
            MigrateReferenceConfigsIfNeeded();

            _scriptRegistry.Clear();

            progress?.Report("正在收集脚本文件...");

            // 收集所有脚本文件
            var scriptFiles = new List<string>();
            await Task.Run(() => CollectScriptFiles(_userScriptsFolder, scriptFiles), cancellationToken);

            if (scriptFiles.Count == 0)
            {
                progress?.Report("未找到脚本文件");
                return;
            }

            progress?.Report($"找到 {scriptFiles.Count} 个脚本文件，开始并行解析...");

            // 使用并行处理来解析脚本
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            var tasks = scriptFiles.Select(async filePath =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await Task.Run(() => RegisterRevivalScript(filePath), cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            progress?.Report($"脚本解析完成，成功注册 {_scriptRegistry.Count} 个脚本");
        }

        /// <summary>
        /// 递归扫描目录
        /// </summary>
        private void ScanDirectory(string directory)
        {
            try
            {
                // 扫描C#脚本文件
                foreach (var file in Directory.GetFiles(directory, "*.cs"))
                {
                    RegisterRevivalScript(file);
                }

                // 递归扫描子目录
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    // 跳过编译输出目录和资源目录
                    var dirName = Path.GetFileName(subDir);
                    if (dirName == "compiled" || dirName == "rivivalresources")
                        continue;

                    ScanDirectory(subDir);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"[脚本目录扫描失败] {directory}: {ex.Message}";
                Debug.WriteLine(errorMsg);
                Trace.WriteLine(errorMsg);
                Console.WriteLine(errorMsg);
            }
        }

        /// <summary>
        /// 收集脚本文件（用于多线程处理）
        /// </summary>
        private void CollectScriptFiles(string directory, List<string> scriptFiles)
        {
            try
            {
                // 收集C#脚本文件
                scriptFiles.AddRange(Directory.GetFiles(directory, "*.cs"));

                // 递归收集子目录
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    // 跳过编译输出目录和资源目录
                    var dirName = Path.GetFileName(subDir);
                    if (dirName == "compiled" || dirName == "rivivalresources")
                        continue;

                    CollectScriptFiles(subDir, scriptFiles);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"[脚本文件收集失败] {directory}: {ex.Message}";
                Debug.WriteLine(errorMsg);
                Trace.WriteLine(errorMsg);
                Console.WriteLine(errorMsg);
            }
        }

        /// <summary>
        /// 注册Revival Script文件
        /// </summary>
        private void RegisterRevivalScript(string filePath)
        {
            try
            {
                var scriptInfo = ParseRevivalScriptInfo(filePath);
                if (scriptInfo != null)
                {
                    scriptInfo.FilePath = filePath;
                    scriptInfo.LastModified = File.GetLastWriteTime(filePath);

                    var relativePath = Path.GetRelativePath(_userScriptsFolder, filePath);
                    _scriptRegistry[relativePath] = scriptInfo;

                    // 自动为脚本创建资源文件夹
                    EnsureScriptResourceFolder(filePath);

                }
                else
                {
                }
            }
            catch (Exception ex)
            {
                var scriptName = Path.GetFileName(filePath);
                var errorMsg = $"[脚本注册失败] {scriptName}: {ex.Message}";
                Debug.WriteLine(errorMsg);
                Trace.WriteLine(errorMsg);
                Console.WriteLine(errorMsg);
            }
        }

        /// <summary>
        /// 解析Revival Script信息
        /// </summary>
        private RevivalScriptInfo? ParseRevivalScriptInfo(string filePath)
        {
            try
            {
                Console.WriteLine($"[脚本解析] 开始解析脚本: {filePath}");

                // 先尝试编译脚本以获取类型信息
                var sourceCode = File.ReadAllText(filePath);

                // 为每个脚本生成唯一的临时程序集名称
                var scriptName = Path.GetFileNameWithoutExtension(filePath);
                var tempAssemblyName = $"TempAssembly_{scriptName}_{Guid.NewGuid():N}";

                var compilation = CSharpCompilation.Create(
                    tempAssemblyName,
                    new[] { CSharpSyntaxTree.ParseText(sourceCode) },
                    GetReferences(filePath),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

                using var ms = new MemoryStream();
                var emitResult = compilation.Emit(ms);

                if (!emitResult.Success)
                {
                    var scriptFileName = Path.GetFileName(filePath);
                    Debug.WriteLine($"[脚本解析失败] {scriptFileName}");
                    Trace.WriteLine($"[脚本解析失败] {scriptFileName}");
                    Console.WriteLine($"[脚本解析失败] {scriptFileName}");

                    foreach (var error in emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        var errorMsg = $"  错误: {error}";
                        Debug.WriteLine(errorMsg);
                        Trace.WriteLine(errorMsg);
                        Console.WriteLine(errorMsg);
                    }
                    return null;
                }


                // 加载程序集并查找脚本类
                var assembly = Assembly.Load(ms.ToArray());
                var scriptType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IRevivalScript).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (scriptType == null)
                {
                    return null;
                }

                // 解析Revival Script特性信息
                var revivalScriptAttr = scriptType.GetCustomAttribute<RevivalScriptAttribute>();
                if (revivalScriptAttr == null)
                {
                    return null;
                }

                var scriptInfo = new RevivalScriptInfo
                {
                    Name = revivalScriptAttr.Name,
                    Author = revivalScriptAttr.Author,
                    Description = revivalScriptAttr.Description,
                    Version = revivalScriptAttr.Version,
                    Category = revivalScriptAttr.Category,
                    Color = revivalScriptAttr.Color
                };

                // 解析端口定义
                try
                {
                    // 创建临时实例来获取端口定义
                    var tempInstance = Activator.CreateInstance(scriptType) as IRevivalScript;
                    if (tempInstance != null)
                    {
                        scriptInfo.InputPorts = tempInstance.GetInputPorts();
                        scriptInfo.OutputPorts = tempInstance.GetOutputPorts();


                        // 调试输出端口信息，特别是灵活端口
                        foreach (var port in scriptInfo.InputPorts)
                        {
                        }
                        foreach (var port in scriptInfo.OutputPorts)
                        {
                        }
                    }
                }
                catch (Exception ex)
                {
                }

                // 解析参数信息
                var properties = scriptType.GetProperties()
                    .Where(p => p.GetCustomAttribute<ScriptParameterAttribute>() != null)
                    .OrderBy(p => p.GetCustomAttribute<ScriptParameterAttribute>()?.Order ?? 0);

                foreach (var prop in properties)
                {
                    var paramAttr = prop.GetCustomAttribute<ScriptParameterAttribute>()!;
                    var paramDef = new RevivalScriptParameterDefinition
                    {
                        Name = prop.Name,
                        DisplayName = string.IsNullOrEmpty(paramAttr.DisplayName) ? prop.Name : paramAttr.DisplayName,
                        Description = paramAttr.Description,
                        Order = paramAttr.Order,
                        PropertyType = prop.PropertyType,
                        DefaultValue = GetDefaultValue(prop.PropertyType)
                    };

                    scriptInfo.Parameters[prop.Name] = paramDef;
                }

                Console.WriteLine($"[脚本解析成功] {scriptInfo.Name} - 输入端口: {scriptInfo.InputPorts.Count} 输出端口: {scriptInfo.OutputPorts.Count} 参数: {scriptInfo.Parameters.Count}");
                return scriptInfo;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取属性类型的默认值
        /// </summary>
        private object GetDefaultValue(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type)!;

            if (type == typeof(string))
                return string.Empty;

            return null!;
        }

        /// <summary>
        /// 获取编译引用
        /// </summary>
        private List<MetadataReference> GetReferences(string? scriptFilePath = null)
        {
            return _assemblyReferenceManager.GetReferences(scriptFilePath);
        }



        /// <summary>
        /// 获取所有可用的Revival Scripts
        /// </summary>
        public Dictionary<string, RevivalScriptInfo> GetAvailableRevivalScripts()
        {
            return new Dictionary<string, RevivalScriptInfo>(_scriptRegistry);
        }

        /// <summary>
        /// 编译所有Revival Scripts
        /// </summary>
        public void CompileRevivalScripts()
        {
            foreach (var kvp in _scriptRegistry)
            {
                var relativePath = kvp.Key;
                var scriptInfo = kvp.Value;

                // 检查是否需要重新编译
                if (NeedsRecompilation(scriptInfo))
                {
                    var result = CompileRevivalScript(scriptInfo);

                    if (result.Success)
                    {
                        scriptInfo.IsCompiled = true;
                        scriptInfo.CompiledAssemblyPath = result.AssemblyPath;

                        // 不再缓存实例，每次都创建新的
                    }
                    else
                    {
                        var scriptName = Path.GetFileName(scriptInfo.FilePath);
                        Debug.WriteLine($"[脚本编译失败] {scriptName}");
                        Trace.WriteLine($"[脚本编译失败] {scriptName}");
                        Console.WriteLine($"[脚本编译失败] {scriptName}");

                        foreach (var error in result.Errors)
                        {
                            var errorMsg = $"  错误: {error}";
                            Debug.WriteLine(errorMsg);
                            Trace.WriteLine(errorMsg);
                            Console.WriteLine(errorMsg);
                        }
                        foreach (var warning in result.Warnings)
                        {
                            var warningMsg = $"  警告: {warning}";
                            Debug.WriteLine(warningMsg);
                            Trace.WriteLine(warningMsg);
                            Console.WriteLine(warningMsg);
                        }
                    }
                }
                else
                {
                    // 标记为已编译，但不缓存实例
                    if (!string.IsNullOrEmpty(scriptInfo.CompiledAssemblyPath) &&
                        File.Exists(scriptInfo.CompiledAssemblyPath))
                    {
                        scriptInfo.IsCompiled = true;
                    }
                }
            }

            _isInitialized = true;
            
            // 触发脚本编译完成事件
            ScriptsCompilationCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 异步编译所有Revival Scripts（多线程版本）
        /// </summary>
        public async Task CompileRevivalScriptsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var scriptsToCompile = _scriptRegistry.Values
                .Where(NeedsRecompilation)
                .ToList();

            if (scriptsToCompile.Count == 0)
            {
                progress?.Report("所有脚本都是最新的，无需编译");
                _isInitialized = true;
                return;
            }

            progress?.Report($"需要编译 {scriptsToCompile.Count} 个脚本，开始并行编译...");

            var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            var compiledCount = 0;
            var failedCount = 0;

            var tasks = scriptsToCompile.Select(async scriptInfo =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await Task.Run(() => CompileRevivalScript(scriptInfo), cancellationToken);

                    if (result.Success)
                    {
                        scriptInfo.IsCompiled = true;
                        scriptInfo.CompiledAssemblyPath = result.AssemblyPath;
                        Interlocked.Increment(ref compiledCount);
                    }
                    else
                    {
                        var scriptName = Path.GetFileName(scriptInfo.FilePath);
                        Debug.WriteLine($"[脚本编译失败] {scriptName}");
                        Trace.WriteLine($"[脚本编译失败] {scriptName}");

                        foreach (var error in result.Errors)
                        {
                            var errorMsg = $"  错误: {error}";
                            Debug.WriteLine(errorMsg);
                            Trace.WriteLine(errorMsg);
                            Console.WriteLine(errorMsg);
                        }
                        foreach (var warning in result.Warnings)
                        {
                            var warningMsg = $"  警告: {warning}";
                            Debug.WriteLine(warningMsg);
                            Trace.WriteLine(warningMsg);
                            Console.WriteLine(warningMsg);
                        }
                        Interlocked.Increment(ref failedCount);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // 标记未需要编译的脚本为已编译
            foreach (var kvp in _scriptRegistry)
            {
                var scriptInfo = kvp.Value;
                if (!NeedsRecompilation(scriptInfo))
                {
                    if (!string.IsNullOrEmpty(scriptInfo.CompiledAssemblyPath) &&
                        File.Exists(scriptInfo.CompiledAssemblyPath))
                    {
                        scriptInfo.IsCompiled = true;
                    }
                }
            }

            _isInitialized = true;
            progress?.Report($"编译完成：成功 {compiledCount} 个，失败 {failedCount} 个");
            
            // 触发脚本编译完成事件
            ScriptsCompilationCompleted?.Invoke(this, EventArgs.Empty);
        }

        // 移除了LoadCompiledRevivalScript方法，不再缓存实例

        /// <summary>
        /// 获取Revival Script信息
        /// </summary>
        public RevivalScriptInfo? GetRevivalScriptInfo(string relativePath)
        {
            return _scriptRegistry.TryGetValue(relativePath, out var info) ? info : null;
        }

        // 移除了GetCompiledRevivalScript方法，不再缓存实例

        /// <summary>
        /// 创建Revival Script实例 - 每次都创建新的独立实例
        /// </summary>
        public IRevivalScript? CreateRevivalScriptInstance(string relativePath)
        {

            if (!_scriptRegistry.TryGetValue(relativePath, out var scriptInfo))
            {
                return null;
            }


            // 检查是否需要编译
            if (!scriptInfo.IsCompiled || NeedsRecompilation(scriptInfo))
            {
                var result = CompileRevivalScript(scriptInfo);

                if (result.Success)
                {
                    scriptInfo.IsCompiled = true;
                    scriptInfo.CompiledAssemblyPath = result.AssemblyPath;
                }
                else
                {
                    var scriptName = Path.GetFileName(scriptInfo.FilePath);
                    Debug.WriteLine($"[脚本实例创建失败] {scriptName}");
                    Trace.WriteLine($"[脚本实例创建失败] {scriptName}");

                    foreach (var error in result.Errors)
                    {
                        var errorMsg = $"  错误: {error}";
                        Debug.WriteLine(errorMsg);
                        Trace.WriteLine(errorMsg);
                        Console.WriteLine(errorMsg);
                    }
                    foreach (var warning in result.Warnings)
                    {
                        var warningMsg = $"  警告: {warning}";
                        Debug.WriteLine(warningMsg);
                        Trace.WriteLine(warningMsg);
                        Console.WriteLine(warningMsg);
                    }
                    return null;
                }
            }

            // 每次都从程序集创建新的实例
            if (!string.IsNullOrEmpty(scriptInfo.CompiledAssemblyPath) &&
                File.Exists(scriptInfo.CompiledAssemblyPath))
            {
                var instance = LoadRevivalScriptFromAssembly(scriptInfo.CompiledAssemblyPath);
                if (instance != null)
                {
                    return instance;
                }
            }

            return null;
        }

        /// <summary>
        /// 检查是否需要重新编译
        /// </summary>
        private bool NeedsRecompilation(RevivalScriptInfo scriptInfo)
        {
            var relativePath = Path.GetRelativePath(_userScriptsFolder, scriptInfo.FilePath);

            // 检查编译缓存中是否有记录
            if (_compilationCache.CompiledScripts.TryGetValue(relativePath, out var cacheEntry))
            {
                // 检查源文件修改时间是否变化
                var currentLastWrite = File.GetLastWriteTime(scriptInfo.FilePath);
                if (cacheEntry.LastModified == currentLastWrite)
                {
                    // 检查编译文件是否存在
                    var expectedAssemblyPath = Path.Combine(_compiledFolder, cacheEntry.AssemblyFileName);
                    if (File.Exists(expectedAssemblyPath))
                    {
                        // 更新脚本信息
                        scriptInfo.IsCompiled = true;
                        scriptInfo.CompiledAssemblyPath = expectedAssemblyPath;
                        return false; // 不需要重新编译
                    }
                }
            }

            return true; // 需要编译
        }

        /// <summary>
        /// 编译Revival Script
        /// </summary>
        private RevivalScriptCompilationResult CompileRevivalScript(RevivalScriptInfo scriptInfo)
        {
            var result = new RevivalScriptCompilationResult();

            try
            {
                Console.WriteLine($"[脚本编译] 开始编译脚本: {scriptInfo.FilePath}");
                var sourceCode = File.ReadAllText(scriptInfo.FilePath);
                var relativePath = Path.GetRelativePath(_userScriptsFolder, scriptInfo.FilePath);

                // 生成固定的程序集名称（不包含时间戳）
                var fileName = Path.GetFileNameWithoutExtension(scriptInfo.FilePath);
                var assemblyName = $"RevivalScript_{fileName}";
                var assemblyFileName = $"{assemblyName}.dll";
                var outputPath = Path.Combine(_compiledFolder, assemblyFileName);

                // 编译选项
                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    new[] { CSharpSyntaxTree.ParseText(sourceCode) },
                    GetReferences(scriptInfo.FilePath),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

                // 编译到内存
                using var ms = new MemoryStream();
                var emitResult = compilation.Emit(ms);

                if (emitResult.Success)
                {
                    // 保存到文件
                    File.WriteAllBytes(outputPath, ms.ToArray());

                    result.Success = true;
                    result.AssemblyPath = outputPath;
                    Console.WriteLine($"[脚本编译成功] {scriptInfo.FilePath} -> {outputPath}");

                    // 更新编译缓存（线程安全）
                    var cacheEntry = new CompilationCacheEntry
                    {
                        ScriptPath = relativePath,
                        AssemblyFileName = assemblyFileName,
                        LastModified = File.GetLastWriteTime(scriptInfo.FilePath),
                        CompiledAt = DateTime.Now
                    };
                    lock (_compilationCacheLock)
                    {
                        _compilationCache.CompiledScripts[relativePath] = cacheEntry;
                    }
                    SaveCompilationCache();

                    // 输出编译成功信息
                }
                else
                {
                    result.Success = false;
                    result.Errors = emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.ToString())
                        .ToList();
                    result.Warnings = emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Warning)
                        .Select(d => d.ToString())
                        .ToList();

                    // 输出编译失败信息
                    var scriptName = Path.GetFileName(scriptInfo.FilePath);
                    Debug.WriteLine($"[脚本编译失败] {scriptName}");
                    Trace.WriteLine($"[脚本编译失败] {scriptName}");
                    Console.WriteLine($"[脚本编译失败] {scriptName}");

                    foreach (var error in result.Errors)
                    {
                        var errorMsg = $"  错误: {error}";
                        Debug.WriteLine(errorMsg);
                        Trace.WriteLine(errorMsg);
                        Console.WriteLine(errorMsg);
                    }
                    foreach (var warning in result.Warnings)
                    {
                        var warningMsg = $"  警告: {warning}";
                        Debug.WriteLine(warningMsg);
                        Trace.WriteLine(warningMsg);
                        Console.WriteLine(warningMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"编译异常: {ex.Message}");

                // 输出编译异常信息
                var scriptName = Path.GetFileName(scriptInfo.FilePath);
                var exceptionMsg = $"[脚本编译异常] {scriptName}: {ex.Message}";
                Debug.WriteLine(exceptionMsg);
                Trace.WriteLine(exceptionMsg);

                if (ex.StackTrace != null)
                {
                    var stackTraceMsg = $"  堆栈跟踪: {ex.StackTrace}";
                    Debug.WriteLine(stackTraceMsg);
                    Trace.WriteLine(stackTraceMsg);
                }
            }

            return result;
        }

        /// <summary>
        /// 从程序集加载Revival Script
        /// </summary>
        private IRevivalScript? LoadRevivalScriptFromAssembly(string assemblyPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);

                var allTypes = assembly.GetTypes();

                var scriptType = allTypes
                    .FirstOrDefault(t => typeof(IRevivalScript).IsAssignableFrom(t) && !t.IsAbstract);

                if (scriptType != null)
                {
                    var instance = Activator.CreateInstance(scriptType) as IRevivalScript;
                    return instance;
                }
                else
                {
                    foreach (var type in allTypes)
                    {
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        /// <summary>
        /// 添加全局程序集引用
        /// </summary>
        public bool AddGlobalAssemblyReference(string assemblyPath)
        {
            return _assemblyReferenceManager.AddGlobalReference(assemblyPath);
        }

        /// <summary>
        /// 移除全局程序集引用
        /// </summary>
        public bool RemoveGlobalAssemblyReference(string assemblyPath)
        {
            return _assemblyReferenceManager.RemoveGlobalReference(assemblyPath);
        }

        /// <summary>
        /// 获取所有全局程序集引用
        /// </summary>
        public List<string> GetGlobalAssemblyReferences()
        {
            return _assemblyReferenceManager.GetGlobalReferences();
        }

        /// <summary>
        /// 为脚本创建引用配置文件
        /// </summary>
        public bool CreateScriptAssemblyReferencesConfig(string scriptFilePath, List<string> references)
        {
            return _assemblyReferenceManager.CreateScriptReferencesConfig(scriptFilePath, references);
        }

        /// <summary>
        /// 获取程序集引用管理器（用于高级操作）
        /// </summary>
        public AssemblyReferenceManager GetAssemblyReferenceManager()
        {
            return _assemblyReferenceManager;
        }

        /// <summary>
        /// 确保脚本资源文件夹存在
        /// </summary>
        private void EnsureScriptResourceFolder(string scriptFilePath)
        {
            try
            {
                var scriptName = Path.GetFileNameWithoutExtension(scriptFilePath);
                var resourceFolder = Path.Combine(_userResourcesFolder, scriptName);

                if (!Directory.Exists(resourceFolder))
                {
                    Directory.CreateDirectory(resourceFolder);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 获取脚本的资源文件夹路径
        /// </summary>
        public string GetScriptResourceFolder(string scriptFilePath)
        {
            return _assemblyReferenceManager.GetScriptResourceFolder(scriptFilePath);
        }

        /// <summary>
        /// 为所有已注册的脚本创建资源文件夹
        /// </summary>
        public void EnsureAllScriptResourceFolders()
        {

            foreach (var kvp in _scriptRegistry)
            {
                var scriptInfo = kvp.Value;
                EnsureScriptResourceFolder(scriptInfo.FilePath);
            }

        }

        /// <summary>
        /// 检查并执行引用配置文件迁移
        /// </summary>
        private void MigrateReferenceConfigsIfNeeded()
        {
            try
            {
                var migrator = new ReferenceConfigMigrator(_userScriptsFolder, _userResourcesFolder);

                if (migrator.NeedsMigration())
                {
                    var result = migrator.MigrateReferenceConfigs();

                    if (result.Success)
                    {
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(result.ErrorMessage))
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 加载编译缓存
        /// </summary>
        private void LoadCompilationCache()
        {
            try
            {
                if (File.Exists(_compilationCacheFile))
                {
                    var json = File.ReadAllText(_compilationCacheFile);
                    var cache = JsonSerializer.Deserialize<CompilationCache>(json);
                    if (cache != null)
                    {
                        _compilationCache = cache;
                    }
                }
            }
            catch (Exception ex)
            {
                _compilationCache = new CompilationCache();
            }
        }

        /// <summary>
        /// 保存编译缓存（线程安全）
        /// </summary>
        private void SaveCompilationCache()
        {
            try
            {
                lock (_compilationCacheLock)
                {
                    var json = JsonSerializer.Serialize(_compilationCache, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(_compilationCacheFile, json);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 清理无效的编译文件
        /// </summary>
        private void CleanupInvalidCompiledFiles()
        {
            try
            {
                if (!Directory.Exists(_compiledFolder))
                    return;

                var validAssemblyFiles = new HashSet<string>();

                // 收集所有有效的程序集文件名
                foreach (var cacheEntry in _compilationCache.CompiledScripts.Values)
                {
                    validAssemblyFiles.Add(cacheEntry.AssemblyFileName);
                }

                // 删除不在缓存中的编译文件（排除缓存文件本身）
                foreach (var file in Directory.GetFiles(_compiledFolder, "*.dll"))
                {
                    var fileName = Path.GetFileName(file);
                    if (!validAssemblyFiles.Contains(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
    }

    /// <summary>
    /// Revival Script编译结果类
    /// </summary>
    public class RevivalScriptCompilationResult
    {
        public bool Success { get; set; }
        public string? AssemblyPath { get; set; }
        public IRevivalScript? ScriptInstance { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Revival Script信息类
    /// </summary>
    public class RevivalScriptInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public string Category { get; set; } = "通用";
        public string Color { get; set; } = "#4A90E2";
        public string FilePath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public bool IsCompiled { get; set; }
        public string? CompiledAssemblyPath { get; set; }
        public Dictionary<string, PortDefinition> InputPorts { get; set; } = new();
        public Dictionary<string, PortDefinition> OutputPorts { get; set; } = new();
        public Dictionary<string, RevivalScriptParameterDefinition> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Revival Script参数定义
    /// </summary>
    public class RevivalScriptParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
        public Type PropertyType { get; set; } = typeof(object);
        public object DefaultValue { get; set; } = null!;
    }

    /// <summary>
    /// 编译缓存类
    /// </summary>
    public class CompilationCache
    {
        public Dictionary<string, CompilationCacheEntry> CompiledScripts { get; set; } = new();
    }

    /// <summary>
    /// 编译缓存条目
    /// </summary>
    public class CompilationCacheEntry
    {
        public string ScriptPath { get; set; } = string.Empty;
        public string AssemblyFileName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public DateTime CompiledAt { get; set; }
    }
}
