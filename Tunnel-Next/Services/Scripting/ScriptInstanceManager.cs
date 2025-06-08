using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Tunnel_Next.Models;
using Tunnel_Next.ViewModels;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// 鲁棒的脚本实例管理器 - 确保实例一致性和参数同步
    /// </summary>
    public class ScriptInstanceManager : INotifyPropertyChanged, IDisposable
    {
        private static readonly Lazy<ScriptInstanceManager> _instance = new(() => new ScriptInstanceManager());
        public static ScriptInstanceManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<int, ScriptInstanceInfo> _instances;
        private readonly ConcurrentDictionary<string, int> _scriptInstanceCounters;
        private readonly SynchronizationContext _uiContext;
        private readonly object _lockObject = new();
        // 旧的节点ID计数器已移除，等待重构
        private bool _disposed;

        private ScriptInstanceManager()
        {
            _instances = new ConcurrentDictionary<int, ScriptInstanceInfo>();
            _scriptInstanceCounters = new ConcurrentDictionary<string, int>();
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        /// <summary>
        /// 创建新的脚本实例
        /// </summary>
        public async Task<ScriptInstanceInfo?> CreateInstanceAsync(string scriptPath, string scriptName)
        {
            try
            {

                // 创建脚本实例
                var scriptInstance = await CreateScriptInstanceAsync(scriptPath);
                if (scriptInstance == null)
                {
                    return null;
                }

                // 生成唯一的节点ID
                var nodeId = NodeIdManager.Instance.GenerateNodeId();

                // 创建ViewModel
                var viewModel = scriptInstance.CreateViewModel();
                if (viewModel != null)
                {
                    viewModel.NodeId = nodeId;
                    await viewModel.InitializeAsync();
                }

                // 创建实例信息
                var instanceInfo = new ScriptInstanceInfo
                {
                    NodeId = nodeId,
                    ScriptPath = scriptPath,
                    ScriptName = scriptName,
                    DisplayName = scriptName,
                    ScriptInstance = scriptInstance,
                    ViewModel = viewModel,
                    CreatedAt = DateTime.Now,
                    LastModified = DateTime.Now
                };

                // 注册实例
                _instances[nodeId] = instanceInfo;

                // 绑定参数同步事件
                BindParameterSynchronization(instanceInfo);

                return instanceInfo;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取脚本实例信息
        /// </summary>
        public ScriptInstanceInfo? GetInstance(int nodeId)
        {
            return _instances.TryGetValue(nodeId, out var instance) ? instance : null;
        }

        /// <summary>
        /// 获取鲁棒的脚本实例 - 多重回退机制
        /// </summary>
        public IRevivalScript? GetRobustScriptInstance(Node node)
        {
            try
            {
                // 方法1: 从实例管理器获取
                if (_instances.TryGetValue(node.Id, out var instanceInfo))
                {
                    return instanceInfo.ScriptInstance;
                }

                // 方法2: 从节点Tag获取ViewModel
                if (node.Tag is IScriptViewModel viewModel)
                {
                    return viewModel.Script;
                }

                // 方法3: 从节点Tag直接获取脚本
                if (node.Tag is IRevivalScript script)
                {
                    return script;
                }

                // 方法4: 尝试重新创建实例
                return TryRecreateInstance(node);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 确保参数同步
        /// </summary>
        public void EnsureParameterSynchronization(IRevivalScript scriptInstance, Node node)
        {
            try
            {
                // 如果有实例信息，确保参数同步
                if (_instances.TryGetValue(node.Id, out var instanceInfo))
                {
                    // 从ViewModel同步参数到脚本实例
                    SyncParametersFromViewModel(instanceInfo);
                }
                else
                {
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 删除实例
        /// </summary>
        public async Task<bool> RemoveInstanceAsync(int nodeId)
        {
            try
            {
                if (_instances.TryRemove(nodeId, out var instanceInfo))
                {
                    // 清理资源
                    await CleanupInstanceAsync(instanceInfo);
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
        /// 获取所有实例
        /// </summary>
        public IEnumerable<ScriptInstanceInfo> GetAllInstances()
        {
            return _instances.Values.ToList();
        }

        /// <summary>
        /// 清理所有实例
        /// </summary>
        public async Task ClearAllInstancesAsync()
        {
            var instances = _instances.Values.ToList();
            _instances.Clear();

            foreach (var instance in instances)
            {
                await CleanupInstanceAsync(instance);
            }

        }

        #region 私有方法

        // 旧的显示名称生成方法已移除，等待重构

        /// <summary>
        /// 创建脚本实例
        /// </summary>
        private Task<IRevivalScript?> CreateScriptInstanceAsync(string scriptPath)
        {
            try
            {
                // 获取全局RevivalScriptManager实例
                var revivalScriptManager = GetRevivalScriptManager();
                if (revivalScriptManager == null)
                {
                    return Task.FromResult<IRevivalScript?>(null);
                }

                // 计算相对路径
                var relativePath = Path.GetRelativePath(revivalScriptManager.UserScriptsFolder, scriptPath);

                // 直接调用RevivalScriptManager创建实例
                var instance = revivalScriptManager.CreateRevivalScriptInstance(relativePath);

                if (instance != null)
                {
                }
                else
                {
                }

                return Task.FromResult(instance);
            }
            catch (Exception ex)
            {
                return Task.FromResult<IRevivalScript?>(null);
            }
        }

        /// <summary>
        /// 获取RevivalScriptManager实例（从应用程序上下文）
        /// </summary>
        private RevivalScriptManager? GetRevivalScriptManager()
        {
            try
            {
                // 从MainWindow获取RevivalScriptManager
                if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow)
                {
                    return mainWindow.GetRevivalScriptManager();
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 绑定参数同步事件
        /// </summary>
        private void BindParameterSynchronization(ScriptInstanceInfo instanceInfo)
        {
            if (instanceInfo.ViewModel != null)
            {
                // 从ViewModel监听PropertyChanged事件
                instanceInfo.ViewModel.PropertyChanged += (sender, e) =>
                {
                    // 参数变化时同步到脚本实例
                    _ = Task.Run(() => SyncParametersFromViewModel(instanceInfo));
                };

                // 移除 ScriptInstanceManager 中的 ParameterExternallyChanged 监听
                // 只保留节点发送事件-主程序监听的单一链路
            }
        }

        // 移除 NotifyNodeGraphNeedsUpdate 方法
        // 不再需要通过 ScriptInstanceManager 通知节点图更新
        // 改为使用单一链路：RevivalScriptBase.ParameterExternallyChanged → NodeEditorViewModel.HandleScriptParameterExternallyChanged

        /// <summary>
        /// 从ViewModel同步参数到脚本实例
        /// </summary>
        private void SyncParametersFromViewModel(ScriptInstanceInfo instanceInfo)
        {
            try
            {
                if (instanceInfo.ViewModel == null || instanceInfo.ScriptInstance == null)
                    return;

                var parameterData = instanceInfo.ViewModel.GetParameterData();
                foreach (var parameter in parameterData)
                {
                    SyncSingleParameter(instanceInfo.ScriptInstance, parameter.Key, parameter.Value);
                }

                instanceInfo.LastModified = DateTime.Now;
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 同步单个参数
        /// </summary>
        private void SyncSingleParameter(IRevivalScript scriptInstance, string parameterName, object value)
        {
            try
            {
                var property = scriptInstance.GetType().GetProperty(parameterName);
                if (property != null && property.CanWrite)
                {
                    var convertedValue = ConvertParameterValue(value, property.PropertyType);
                    property.SetValue(scriptInstance, convertedValue);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 转换参数值类型
        /// </summary>
        private object? ConvertParameterValue(object? value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;

            try
            {
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                }

                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        /// <summary>
        /// 尝试重新创建实例
        /// </summary>
        private IRevivalScript? TryRecreateInstance(Node node)
        {
            try
            {
                // 这里可以实现实例重建逻辑
                // 暂时返回null，表示无法重建
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 清理实例资源
        /// </summary>
        private async Task CleanupInstanceAsync(ScriptInstanceInfo instanceInfo)
        {
            try
            {
                await Task.Run(() =>
                {
                    instanceInfo.ViewModel?.Dispose();

                    if (instanceInfo.ScriptInstance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
            }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _ = Task.Run(async () => await ClearAllInstancesAsync());
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 脚本实例信息
    /// </summary>
    public class ScriptInstanceInfo
    {
        public int NodeId { get; set; }
        public string ScriptPath { get; set; } = string.Empty;
        public string ScriptName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public IRevivalScript? ScriptInstance { get; set; }
        public IScriptViewModel? ViewModel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
