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
using System.Xml.Linq;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// TunnelExtension Scripts管理器 - 管理用户脚本系统
    /// </summary>
    public class TunnelExtensionScriptManager : IDisposable
    {
        private readonly string _userScriptsFolder;
        private readonly string _userResourcesFolder;
        private readonly string _compiledFolder;
        private readonly string _compilationCacheFile;
        private readonly ConcurrentDictionary<string, TunnelExtensionScriptInfo> _scriptRegistry = new();
        private readonly AssemblyReferenceManager _assemblyReferenceManager;
        private CompilationCache _compilationCache = new();
        // 移除了_compiledScripts字段，不再缓存实例
        private volatile bool _isInitialized = false;
        private readonly object _compilationCacheLock = new();
        private FileSystemWatcher? _scriptsWatcher; // 监听脚本改动
        private Timer? _debounceTimer;
        private readonly object _watcherLock = new();
        private const int DebounceDelayMs = 1000;

	        // 自定义类型（Scripts/TunnelExtensionResources/Types）支持
	        private readonly string _typesFolder;
	        private readonly string _typesAssemblyName = "TunnelExtensionTypes";
	        private readonly string _typesAssemblyPath;


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

        public TunnelExtensionScriptManager(string userScriptsFolder, string userResourcesFolder)
        {
            _userScriptsFolder = userScriptsFolder ?? throw new ArgumentNullException(nameof(userScriptsFolder));
            _userResourcesFolder = userResourcesFolder ?? throw new ArgumentNullException(nameof(userResourcesFolder));

            // 验证路径不能为空字符串
            if (string.IsNullOrWhiteSpace(_userScriptsFolder))
                throw new ArgumentException("用户脚本文件夹路径不能为空", nameof(userScriptsFolder));
            if (string.IsNullOrWhiteSpace(_userResourcesFolder))
                throw new ArgumentException("用户资源文件夹路径不能为空", nameof(userResourcesFolder));

            // 添加调试输出
            System.Diagnostics.Debug.WriteLine($"[TunnelExtensionScriptManager] 初始化脚本管理器");
            System.Diagnostics.Debug.WriteLine($"[TunnelExtensionScriptManager] 脚本文件夹: {_userScriptsFolder}");
            System.Diagnostics.Debug.WriteLine($"[TunnelExtensionScriptManager] 资源文件夹: {_userResourcesFolder}");
            Console.WriteLine($"[TunnelExtensionScriptManager] 创建脚本管理器，脚本文件夹: {_userScriptsFolder}");
            _compiledFolder = Path.Combine(_userScriptsFolder, "compiled");
            _compilationCacheFile = Path.Combine(_compiledFolder, "compilation-cache.json");
            _assemblyReferenceManager = new AssemblyReferenceManager(_userScriptsFolder, _userResourcesFolder);

	            // 自定义类型路径与输出
	            _typesFolder = Path.Combine(_userScriptsFolder, "TunnelExtensionResources", "Types");
	            _typesAssemblyPath = Path.Combine(_compiledFolder, _typesAssemblyName + ".dll");


            // 确保文件夹存在
            Directory.CreateDirectory(_userScriptsFolder);
            Directory.CreateDirectory(_userResourcesFolder);
            Directory.CreateDirectory(_compiledFolder);


	            // 初始时尝试编译并注册Types程序集
	            try
	            {
	                CompileAndRegisterTypesAssembly();
	            }
	            catch { }

            // 加载编译缓存
            LoadCompilationCache();

            // 清理无效的编译文件
            CleanupInvalidCompiledFiles();

            InitializeScriptsWatcher();
        }

        /// <summary>
        /// 扫描TunnelExtension Scripts
        /// </summary>
        public void ScanTunnelExtensionScripts()
        {
            // 检查并执行引用配置文件迁移
            MigrateReferenceConfigsIfNeeded();

            _scriptRegistry.Clear();

            // 递归扫描所有.cs文件
            ScanDirectory(_userScriptsFolder);
        }

        /// <summary>
        /// 异步扫描TunnelExtension Scripts（多线程版本）
        /// </summary>
        public async Task ScanTunnelExtensionScriptsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
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
                    await Task.Run(() => RegisterTunnelExtensionScript(filePath), cancellationToken);
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
                    RegisterTunnelExtensionScript(file);
                }

                // 扫描符号节点文件
                foreach (var file in Directory.GetFiles(directory, "*.sn"))
                {
                    RegisterTunnelExtensionScript(file);
                }

                // 递归扫描子目录
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    // 跳过编译输出目录和资源目录
                    var dirName = Path.GetFileName(subDir);
                    if (dirName == "compiled" || dirName == "TunnelExtensionResources")
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
                scriptFiles.AddRange(Directory.GetFiles(directory, "*.sn"));

                // 递归收集子目录
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    // 跳过编译输出目录和资源目录
                    var dirName = Path.GetFileName(subDir);
                    if (dirName == "compiled" || dirName == "TunnelExtensionResources")
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
        /// 注册TunnelExtension Script文件
        /// </summary>
        private void RegisterTunnelExtensionScript(string filePath)
        {
            try
            {
                TunnelExtensionScriptInfo? scriptInfo = null;

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".cs")
                {
                    scriptInfo = ParseTunnelExtensionScriptInfo(filePath);
                }
                else if (ext == ".sn")
                {
                    scriptInfo = ParseSymbolNodeInfo(filePath);
                }

                if (scriptInfo != null)
                {
                    scriptInfo.FilePath = filePath;
                    scriptInfo.LastModified = File.GetLastWriteTime(filePath);

                    var relativePath = Path.GetRelativePath(_userScriptsFolder, filePath);
                    _scriptRegistry[relativePath] = scriptInfo;

                    // 自动为脚本创建资源文件夹（仅对脚本有效）
                    if (!scriptInfo.IsSymbolNode)
                    {
                        EnsureScriptResourceFolder(filePath);
                    }

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
        /// 解析TunnelExtension Script信息
        /// </summary>
        private TunnelExtensionScriptInfo? ParseTunnelExtensionScriptInfo(string filePath)
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

                // 应用源码生成器（如有）
                try
                {
                    var generators = LoadSourceGeneratorsForScript(filePath);
                    if (generators.Count > 0)
                    {
                        Debug.WriteLine($"[源码生成器] {scriptName}: 将加载 {generators.Count} 个生成器");
                        Trace.WriteLine($"[源码生成器] {scriptName}: 将加载 {generators.Count} 个生成器");
                        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
                        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
                        compilation = (CSharpCompilation)outputCompilation;
                        foreach (var diag in generatorDiagnostics)
                        {
                            Debug.WriteLine($"[源码生成器诊断] {diag}");
                            Trace.WriteLine($"[源码生成器诊断] {diag}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[源码生成器] {scriptName}: 未解析到任何生成器");
                        Trace.WriteLine($"[源码生成器] {scriptName}: 未解析到任何生成器");
                    }
                }
                catch { }

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
                    .FirstOrDefault(t => typeof(ITunnelExtensionScript).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (scriptType == null)
                {
                    return null;
                }

                // 解析TunnelExtension Script特性信息
                var TunnelExtensionScriptAttr = scriptType.GetCustomAttribute<TunnelExtensionScriptAttribute>();
                if (TunnelExtensionScriptAttr == null)
                {
                    return null;
                }

                var scriptInfo = new TunnelExtensionScriptInfo
                {
                    Name = TunnelExtensionScriptAttr.Name,
                    Author = TunnelExtensionScriptAttr.Author,
                    Description = TunnelExtensionScriptAttr.Description,
                    Version = TunnelExtensionScriptAttr.Version,
                    Category = TunnelExtensionScriptAttr.Category,
                    Color = TunnelExtensionScriptAttr.Color
                };

                // 解析端口定义
                try
                {
                    // 创建临时实例来获取端口定义
                    var tempInstance = Activator.CreateInstance(scriptType) as ITunnelExtensionScript;
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
                    var paramDef = new TunnelExtensionScriptParameterDefinition
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
        /// 解析符号节点信息
        /// </summary>
        private TunnelExtensionScriptInfo? ParseSymbolNodeInfo(string filePath)
        {
            try
            {
                // 使用简单XML格式解析符号节点定义
                var doc = XDocument.Load(filePath);
                var root = doc.Root;
                if (root == null || root.Name.LocalName != "SymbolNode")
                {
                    return null;
                }

                var info = new TunnelExtensionScriptInfo
                {
                    Name = root.Attribute("Name")?.Value ?? Path.GetFileNameWithoutExtension(filePath),
                    Description = root.Attribute("Description")?.Value ?? string.Empty,
                    Category = root.Attribute("Category")?.Value ?? "符号节点",
                    Color = root.Attribute("Color")?.Value ?? "#FF0000",
                    IsCompiled = true, // 不需要编译
                    IsSymbolNode = true
                };

                // 解析输入端口
                foreach (var portElem in root.Elements("InputPort"))
                {
                    var portName = portElem.Attribute("Name")?.Value;
                    if (string.IsNullOrWhiteSpace(portName)) continue;
                    var dataType = portElem.Attribute("DataType")?.Value ?? "Any";
                    var desc = portElem.Attribute("Description")?.Value ?? string.Empty;
                    var flexibleAttr = portElem.Attribute("IsFlexible")?.Value ?? "false";
                    bool isFlexible = false;
                    bool.TryParse(flexibleAttr, out isFlexible);
                    info.InputPorts[portName] = new PortDefinition(dataType, isFlexible, desc);
                }

                // 解析输出端口
                foreach (var portElem in root.Elements("OutputPort"))
                {
                    var portName = portElem.Attribute("Name")?.Value;
                    if (string.IsNullOrWhiteSpace(portName)) continue;
                    var dataType = portElem.Attribute("DataType")?.Value ?? "Any";
                    var desc = portElem.Attribute("Description")?.Value ?? string.Empty;
                    var flexibleAttr = portElem.Attribute("IsFlexible")?.Value ?? "false";
                    bool isFlexible = false;
                    bool.TryParse(flexibleAttr, out isFlexible);
                    info.OutputPorts[portName] = new PortDefinition(dataType, isFlexible, desc);
                }

                return info;
            }
            catch (Exception)
            {
                // 解析失败则返回null
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
        /// 获取所有可用的TunnelExtension Scripts
        /// </summary>
        public Dictionary<string, TunnelExtensionScriptInfo> GetAvailableTunnelExtensionScripts()
        {
            return new Dictionary<string, TunnelExtensionScriptInfo>(_scriptRegistry);
        }

        /// <summary>
        /// 编译所有TunnelExtension Scripts
        /// </summary>
        public void CompileTunnelExtensionScripts()
        {
            foreach (var kvp in _scriptRegistry)
            {
                var relativePath = kvp.Key;
                var scriptInfo = kvp.Value;

                // 检查是否需要重新编译
                if (NeedsRecompilation(scriptInfo))
                {
                    var result = CompileTunnelExtensionScript(scriptInfo);

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
            Console.WriteLine("[TunnelExtensionScriptManager] 脚本编译完成");
            Trace.WriteLine("[TunnelExtensionScriptManager] 脚本编译完成");
            Debug.WriteLine("[TunnelExtensionScriptManager] 脚本编译完成");

            var handler = ScriptsCompilationCompleted;
            if (handler != null)
            {
                Console.WriteLine("[TunnelExtensionScriptManager] 触发脚本编译完成事件");
                handler(this, EventArgs.Empty);
            }
            else
            {
                Console.WriteLine("[TunnelExtensionScriptManager] 脚本编译完成事件没有订阅者");
            }
        }

        /// <summary>
        /// 异步编译所有TunnelExtension Scripts（多线程版本）
        /// </summary>
        public async Task CompileTunnelExtensionScriptsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var scriptsToCompile = _scriptRegistry.Values
                .Where(NeedsRecompilation)
                .ToList();

            if (scriptsToCompile.Count == 0)
            {
                progress?.Report("所有脚本都是最新的，无需编译");
                _isInitialized = true;

                // 即使没有脚本需要编译，也触发编译完成事件
                Console.WriteLine("[TunnelExtensionScriptManager] 所有脚本都是最新的，无需编译");
                var noCompileHandler = ScriptsCompilationCompleted;
                if (noCompileHandler != null)
                {
                    Console.WriteLine("[TunnelExtensionScriptManager] 触发脚本编译完成事件（无需编译情况）");
                    noCompileHandler(this, EventArgs.Empty);
                }
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
                    var result = await Task.Run(() => CompileTunnelExtensionScript(scriptInfo), cancellationToken);

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
            Console.WriteLine("[TunnelExtensionScriptManager] 异步脚本编译完成");
            Trace.WriteLine("[TunnelExtensionScriptManager] 异步脚本编译完成");
            Debug.WriteLine("[TunnelExtensionScriptManager] 异步脚本编译完成");

            var handler = ScriptsCompilationCompleted;
            if (handler != null)
            {
                Console.WriteLine("[TunnelExtensionScriptManager] 触发异步脚本编译完成事件");
                handler(this, EventArgs.Empty);
            }
            else
            {
                Console.WriteLine("[TunnelExtensionScriptManager] 异步脚本编译完成事件没有订阅者");
            }
        }

        // 移除了LoadCompiledTunnelExtensionScript方法，不再缓存实例

        /// <summary>
        /// 获取TunnelExtension Script信息
        /// </summary>
        public TunnelExtensionScriptInfo? GetTunnelExtensionScriptInfo(string relativePath)
        {
            return _scriptRegistry.TryGetValue(relativePath, out var info) ? info : null;
        }

        // 移除了GetCompiledTunnelExtensionScript方法，不再缓存实例

        /// <summary>
        /// 创建TunnelExtension Script实例 - 每次都创建新的独立实例
        /// </summary>
        public ITunnelExtensionScript? CreateTunnelExtensionScriptInstance(string relativePath)
        {

            if (!_scriptRegistry.TryGetValue(relativePath, out var scriptInfo))
            {
                return null;
            }


            // 检查是否需要编译
            if (!scriptInfo.IsCompiled || NeedsRecompilation(scriptInfo))
            {
                var result = CompileTunnelExtensionScript(scriptInfo);

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
                var instance = LoadTunnelExtensionScriptFromAssembly(scriptInfo.CompiledAssemblyPath);
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
        private bool NeedsRecompilation(TunnelExtensionScriptInfo scriptInfo)
        {
            // 符号节点不需要编译
            if (scriptInfo.IsSymbolNode)
            {
                scriptInfo.IsCompiled = true;
                return false;
            }

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
        /// 编译TunnelExtension Script
        /// </summary>
        private TunnelExtensionScriptCompilationResult CompileTunnelExtensionScript(TunnelExtensionScriptInfo scriptInfo)
        {
            var result = new TunnelExtensionScriptCompilationResult();

            try
            {
                Console.WriteLine($"[脚本编译] 开始编译脚本: {scriptInfo.FilePath}");
                var sourceCode = File.ReadAllText(scriptInfo.FilePath);
                var relativePath = Path.GetRelativePath(_userScriptsFolder, scriptInfo.FilePath);

                // 生成固定的程序集名称（不包含时间戳）
                var fileName = Path.GetFileNameWithoutExtension(scriptInfo.FilePath);
                var assemblyName = $"TunnelExtensionScript_{fileName}";
                var assemblyFileName = $"{assemblyName}.dll";
                var outputPath = Path.Combine(_compiledFolder, assemblyFileName);

                // 编译选项
                var compilation = CSharpCompilation.Create(
                    assemblyName,
                    new[] { CSharpSyntaxTree.ParseText(sourceCode) },
                    GetReferences(scriptInfo.FilePath),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

                // 应用源码生成器（如有）
                try
                {
                    var generators = LoadSourceGeneratorsForScript(scriptInfo.FilePath);
                    if (generators.Count > 0)
                    {
                        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators);
                        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
                        compilation = (CSharpCompilation)outputCompilation;
                        foreach (var diag in generatorDiagnostics)
                        {
                            Debug.WriteLine($"[源码生成器诊断] {diag}");
                            Trace.WriteLine($"[源码生成器诊断] {diag}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[源码生成器] {scriptInfo.Name}: 未解析到任何生成器");
                        Trace.WriteLine($"[源码生成器] {scriptInfo.Name}: 未解析到任何生成器");
                    }
                }
                catch { }

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
        /// 从程序集加载TunnelExtension Script
        /// </summary>
        private ITunnelExtensionScript? LoadTunnelExtensionScriptFromAssembly(string assemblyPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);

                var allTypes = assembly.GetTypes();

                var scriptType = allTypes
                    .FirstOrDefault(t => typeof(ITunnelExtensionScript).IsAssignableFrom(t) && !t.IsAbstract);

                if (scriptType != null)
                {
                    var instance = Activator.CreateInstance(scriptType) as ITunnelExtensionScript;

                    // 检查是否实现了基底预览接口，如果是则自动注册
                    if (instance is IBasePreviewProvider basePreviewProvider)
                    {
                        try
                        {
                            var ctx = new ScriptContext("", "", "", () => null, _ => { }, _ => new Dictionary<string, object>(), (a, b, c) => { });
                            UI.PreviewManager.Instance.RegisterBasePreviewProvider(basePreviewProvider, ctx);
                        }
                        catch
                        {
                            // 注册失败时忽略，不影响脚本的正常加载
                        }
                    }

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


	        /// <summary>
	        /// 编译 Scripts/TunnelExtensionResources/Types 下的自定义类型，并注册为全局引用
	        /// </summary>
	        private void CompileAndRegisterTypesAssembly()
	        {
	            try
	            {
	                if (!Directory.Exists(_typesFolder))
	                {
	                    return;
	                }
	                var typeFiles = Directory.GetFiles(_typesFolder, "*.cs", SearchOption.AllDirectories);
	                if (typeFiles.Length == 0)
	                {
	                    return;
	                }

	                var syntaxTrees = typeFiles.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f))).ToArray();
	                var compilation = CSharpCompilation.Create(
	                    _typesAssemblyName,
	                    syntaxTrees,
	                    GetReferences(),
	                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

	                using var ms = new MemoryStream();
	                var emitResult = compilation.Emit(ms);
	                if (!emitResult.Success)
	                {
	                    return;
	                }

	                Directory.CreateDirectory(_compiledFolder);
	                File.WriteAllBytes(_typesAssemblyPath, ms.ToArray());

	                // 将Types程序集添加为全局引用
	                _assemblyReferenceManager.AddGlobalReference(_typesAssemblyPath);
	            }
	            catch (Exception ex)
	            {
	            }
	        }


        private void InitializeScriptsWatcher()
        {
            try
            {
                _scriptsWatcher = new FileSystemWatcher(_userScriptsFolder, "*.cs")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
                };
                _scriptsWatcher.Changed += OnScriptFileChanged;
                _scriptsWatcher.Created += OnScriptFileChanged;
                _scriptsWatcher.Deleted += OnScriptFileChanged;
                _scriptsWatcher.Renamed += OnScriptFileChanged;
                _scriptsWatcher.EnableRaisingEvents = true;
            }
            catch { /* 忽略初始化失败 */ }
        }

        /// <summary>
        /// 从 Scripts/TunnelExtensionResources/{ScriptName}/Generators.json 加载源码生成器并实例化
        /// </summary>
        /// <param name="scriptFilePath">脚本文件的完整路径</param>
        /// <returns>已实例化的源码生成器列表</returns>
        private List<ISourceGenerator> LoadSourceGeneratorsForScript(string scriptFilePath)
        {
            var result = new List<ISourceGenerator>();
            try
            {
                var scriptName = Path.GetFileNameWithoutExtension(scriptFilePath);

                // 尝试定位包含该脚本的“Scripts”根：优先使用用户脚本根；若脚本不在用户根下，则向上搜索名为“Scripts”的祖先目录
                var scriptsRoot = _userScriptsFolder;
                try
                {
                    var scriptFull = Path.GetFullPath(scriptFilePath);
                    var userRootFull = Path.GetFullPath(_userScriptsFolder);
                    if (!scriptFull.StartsWith(userRootFull, StringComparison.OrdinalIgnoreCase))
                    {
                        var dir = Path.GetDirectoryName(scriptFull);
                        while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            if (string.Equals(Path.GetFileName(dir), "Scripts", StringComparison.OrdinalIgnoreCase))
                            {
                                scriptsRoot = dir;
                                break;
                            }
                            var parent = Path.GetDirectoryName(dir);
                            if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase)) break;
                            dir = parent;
                        }
                    }
                }
                catch { }

                // 配置路径位于“{ScriptsRoot}/TunnelExtensionResources/{ScriptName}/Generators.json”
                var configFolder = Path.Combine(scriptsRoot, "TunnelExtensionResources", scriptName);
                var configPath = Path.Combine(configFolder, "Generators.json");
                if (!File.Exists(configPath))
                {
                    Debug.WriteLine($"[源码生成器] {scriptName}: 未找到 Generators.json，跳过");
                    Trace.WriteLine($"[源码生成器] {scriptName}: 未找到 Generators.json，跳过");
                    return result;
                }

                // 读取配置（支持两种格式：{"Generators":["path1","path2"]} 或 ["path1","path2"])）
                var dllPaths = new List<string>();
                try
                {
                    var json = File.ReadAllText(configPath);
                    if (string.IsNullOrWhiteSpace(json)) return result;

                    try
                    {
                        var obj = JsonSerializer.Deserialize<GeneratorsConfig>(json);
                        if (obj?.Generators != null)
                        {
                            dllPaths.AddRange(obj.Generators);
                        }
                    }
                    catch
                    {
                        try
                        {
                            var arr = JsonSerializer.Deserialize<List<string>>(json);
                            if (arr != null) dllPaths.AddRange(arr);
                        }
                        catch { }
                    }
                }
                catch { }

                if (dllPaths.Count == 0)
                {
                    Debug.WriteLine($"[源码生成器] {scriptName}: Generators.json 无有效条目，跳过");
                    Trace.WriteLine($"[源码生成器] {scriptName}: Generators.json 无有效条目，跳过");
                    return result;
                }

                int configured = dllPaths.Count;
                int resolvedCount = 0;
                int assembliesLoaded = 0;
                var unresolved = new List<string>();
                var resolvedEntries = new List<string>();

                foreach (var entry in dllPaths)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(entry)) continue;

                        // 允许仅写 DLL 名称（不含路径），以保证跨机器可用
                        var fileName = Path.GetFileName(entry);
                        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            fileName += ".dll";
                        }

                        bool hasDir = entry.Contains('/') || entry.Contains('\\');

                        // 生成候选路径（优先绝对/相对，随后名称搜索）
                        var candidatePaths = new List<string>();
                        if (Path.IsPathRooted(entry))
                        {
                            candidatePaths.Add(entry);
                        }
                        else if (hasDir)
                        {
                            candidatePaths.Add(Path.Combine(configFolder, entry));
                            candidatePaths.Add(Path.Combine(_userScriptsFolder, entry));
                        }
                        else
                        {
                            candidatePaths.Add(Path.Combine(configFolder, fileName));
                            candidatePaths.Add(Path.Combine(_userScriptsFolder, fileName));
                        }

                        string? resolved = candidatePaths.FirstOrDefault(File.Exists);

                        // 若未直接命中，进一步在常见目录中按文件名递归搜索（资源目录与脚本根目录）
                        if (resolved == null)
                        {
                            Debug.WriteLine($"[源码生成器] {scriptName}: 未在本地目录找到 {fileName}，继续搜索");
                            Trace.WriteLine($"[源码生成器] {scriptName}: 未在本地目录找到 {fileName}，继续搜索");
                            try { resolved = Directory.EnumerateFiles(configFolder, fileName, SearchOption.AllDirectories).FirstOrDefault(); } catch { }
                            if (resolved == null)
                            {
                                try { resolved = Directory.EnumerateFiles(_userScriptsFolder, fileName, SearchOption.AllDirectories).FirstOrDefault(); } catch { }
                            }
                        }

                        // 扩展搜索：应用程序目录、运行时目录、全局 NuGet 缓存等
                        if (resolved == null)
                        {
                            var searchPaths = new List<string>();

                            try
                            {
                                // 应用程序目录
                                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                                if (!string.IsNullOrEmpty(appDir)) searchPaths.Add(appDir);

                                // 运行时目录
                                var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
                                if (!string.IsNullOrEmpty(runtimeDir)) searchPaths.Add(runtimeDir);

                                // NuGet 全局包缓存（如果存在）
                                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                var nugetPackages = Path.Combine(userProfile, ".nuget", "packages");
                                if (Directory.Exists(nugetPackages)) searchPaths.Add(nugetPackages);

                                // 程序文件目录下的 dotnet 共享运行时（Windows）
                                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                {
                                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                                    var dotnetShared = Path.Combine(programFiles, "dotnet", "shared");
                                    if (Directory.Exists(dotnetShared)) searchPaths.Add(dotnetShared);
                                }

                                foreach (var searchPath in searchPaths)
                                {
                                    try
                                    {
                                        resolved = Directory.EnumerateFiles(searchPath, fileName, SearchOption.AllDirectories).FirstOrDefault();
                                        if (resolved != null) break;
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }

                        if (resolved == null)
                        {
                            // 未找到物理文件，放弃该条目（不绑定任何具体路径）
                            Debug.WriteLine($"[源码生成器] {scriptName}: 未找到 {fileName}，跳过该条目");
                            Trace.WriteLine($"[源码生成器] {scriptName}: 未找到 {fileName}，跳过该条目");
                            continue;
                        }
                        else
                        {
                            resolvedCount++;
                            Debug.WriteLine($"[源码生成器] {scriptName}: 解析 {entry} -> {resolved}");
                            Trace.WriteLine($"[源码生成器] {scriptName}: 解析 {entry} -> {resolved}");
                        }

                        // 加载程序集并查找生成器类型
                        Assembly asm;
                        try
                        {
                            asm = Assembly.LoadFrom(resolved);
                        }
                        catch
                        {
                            // 若LoadFrom失败，尝试按名称加载（去掉扩展名）
                            try { asm = Assembly.Load(Path.GetFileNameWithoutExtension(fileName)); }
                            catch { continue; }
                        }

                        foreach (var t in asm.GetTypes())
                        {
                            if (t.IsAbstract || t.IsInterface) continue;
                            try
                            {
                                if (typeof(IIncrementalGenerator).IsAssignableFrom(t))
                                {
                                    if (Activator.CreateInstance(t) is IIncrementalGenerator inc)
                                    {
                                        var wrapped = TryWrapIncrementalGenerator(inc);
                                        if (wrapped != null)
                                        {
                                            result.Add(wrapped);
                                        }
                                    }
                                }
                                else if (typeof(ISourceGenerator).IsAssignableFrom(t))
                                {
                                    var gen = Activator.CreateInstance(t) as ISourceGenerator;
                                    if (gen != null)
                                    {
                                        result.Add(gen);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }

        private class GeneratorsConfig
        {
            public List<string>? Generators { get; set; }
        }

        /// <summary>
        /// 将 IIncrementalGenerator 包装为 ISourceGenerator（简易适配）
        /// </summary>
        private static ISourceGenerator? TryWrapIncrementalGenerator(IIncrementalGenerator inc)
        {
            try
            {
                // 使用扩展方法将增量生成器转换为普通生成器接口
                return inc.AsSourceGenerator();
            }
            catch
            {
                return null;
            }
        }

        private void OnScriptFileChanged(object sender, FileSystemEventArgs e)
        {
            // 跳过编译输出，与资源目录中的绝大多数文件。但Types需要特殊处理
            var relative = Path.GetRelativePath(_userScriptsFolder, e.FullPath);
            if (relative.StartsWith("compiled", StringComparison.OrdinalIgnoreCase)) return;

            // 如果是自定义类型（TunnelExtensionResources/Types），则立即编译类型并触发脚本重编译
            var typesPrefix = Path.Combine("TunnelExtensionResources", "Types");
            if (relative.StartsWith(typesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    CompileAndRegisterTypesAssembly();

                    // 重新编译脚本（使用防抖逻辑避免频繁触发）
                    lock (_watcherLock)
                    {
                        if (_debounceTimer == null)
                        {
                            _debounceTimer = new Timer(_ => HandleScriptsChanged(), null, DebounceDelayMs, Timeout.Infinite);
                        }
                        else
                        {
                            _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
                        }
                    }
                }
                catch { }
                return;
            }

            // 其他资源目录下的变动忽略
            if (relative.StartsWith("TunnelExtensionResources", StringComparison.OrdinalIgnoreCase)) return;

            lock (_watcherLock)
            {
                if (_debounceTimer == null)
                {
                    _debounceTimer = new Timer(_ => HandleScriptsChanged(), null, DebounceDelayMs, Timeout.Infinite);
                }
                else
                {
                    _debounceTimer.Change(DebounceDelayMs, Timeout.Infinite);
                }
            }
        }

        private void HandleScriptsChanged()
        {
            try
            {
                ScanTunnelExtensionScripts();
                CompileTunnelExtensionScripts();
                ScriptsCompilationCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TunnelExtensionScriptManager] 热加载失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _scriptsWatcher?.Dispose();
            _debounceTimer?.Dispose();
        }
    }

    /// <summary>
    /// TunnelExtension Script编译结果类
    /// </summary>
    public class TunnelExtensionScriptCompilationResult
    {
        public bool Success { get; set; }
        public string? AssemblyPath { get; set; }
        public ITunnelExtensionScript? ScriptInstance { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// TunnelExtension Script信息类
    /// </summary>
    public class TunnelExtensionScriptInfo
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
        public Dictionary<string, TunnelExtensionScriptParameterDefinition> Parameters { get; set; } = new();
        public bool IsSymbolNode { get; set; } = false;
    }

    /// <summary>
    /// TunnelExtension Script参数定义
    /// </summary>
    public class TunnelExtensionScriptParameterDefinition
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
