using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Reflection;
using Tunnel_Next.ViewModels;
using Tunnel_Next.Services;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// Event arguments for script parameter changes.
    /// </summary>
    public class ScriptParameterChangedEventArgs : EventArgs
    {
        public string ParameterName { get; }
        public object? NewValue { get; }

        public ScriptParameterChangedEventArgs(string parameterName, object? newValue)
        {
            ParameterName = parameterName;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// Revival Script基类，提供通用功能
    /// </summary>
    public abstract class RevivalScriptBase : IRevivalScript, INotifyPropertyChanged
    {
        /// <summary>
        /// Fired when a parameter is changed externally (e.g., by UI or ViewModel)
        /// and the host application might need to reprocess the node graph.
        /// </summary>
        public event EventHandler<ScriptParameterChangedEventArgs>? ParameterExternallyChanged;

        #region 端口定义（子类必须重写）

        /// <summary>
        /// 获取输入端口定义（子类必须重写）
        /// </summary>
        public abstract Dictionary<string, PortDefinition> GetInputPorts();

        /// <summary>
        /// 获取输出端口定义（子类必须重写）
        /// </summary>
        public abstract Dictionary<string, PortDefinition> GetOutputPorts();

        #endregion

        #region 必须实现的抽象方法

        /// <summary>
        /// 核心处理函数（子类必须实现）
        /// </summary>
        public abstract Dictionary<string, object> Process(Dictionary<string, object> inputs, IScriptContext context);

        /// <summary>
        /// 创建WPF参数控件（子类必须实现）
        /// </summary>
        public abstract FrameworkElement CreateParameterControl();

        /// <summary>
        /// 创建ViewModel（子类必须实现）
        /// </summary>
        public abstract IScriptViewModel CreateViewModel();

        /// <summary>
        /// 参数变化通知（子类必须实现）
        /// </summary>
        public abstract Task OnParameterChangedAsync(string parameterName, object oldValue, object newValue);

        /// <summary>
        /// 序列化参数（子类必须实现）
        /// </summary>
        /// <returns>序列化后的参数字典</returns>
        public abstract Dictionary<string, object> SerializeParameters();

        /// <summary>
        /// 反序列化参数（子类必须实现）
        /// </summary>
        /// <param name="data">参数数据字典</param>
        public abstract void DeserializeParameters(Dictionary<string, object> data);

        #endregion

        #region 可选的虚方法

        /// <summary>
        /// 注入元数据到下游（不覆盖已有键）
        /// 子类可以重写此方法来添加自定义元数据
        /// </summary>
        /// <param name="currentMetadata">当前元数据</param>
        /// <returns>注入后的元数据</returns>
        public virtual Dictionary<string, object> InjectMetadata(Dictionary<string, object> currentMetadata)
        {
            // 默认实现：不注入任何元数据，由子类决定
            return currentMetadata;
        }

        /// <summary>
        /// 从上游提取所需元数据
        /// 子类可以重写此方法来提取和使用上游元数据
        /// </summary>
        /// <param name="upstreamMetadata">上游元数据</param>
        public virtual void ExtractMetadata(Dictionary<string, object> upstreamMetadata)
        {
            // 默认实现：提取常用的元数据信息
            if (upstreamMetadata.ContainsKey("图像尺寸"))
            {
                OnMetadataExtracted("图像尺寸", upstreamMetadata["图像尺寸"]);
            }

            if (upstreamMetadata.ContainsKey("颜色空间"))
            {
                OnMetadataExtracted("颜色空间", upstreamMetadata["颜色空间"]);
            }

            if (upstreamMetadata.ContainsKey("处理历史"))
            {
                OnMetadataExtracted("处理历史", upstreamMetadata["处理历史"]);
            }
        }

        /// <summary>
        /// 强制覆盖元数据键值对
        /// 子类可以重写此方法来生成或覆盖特定的元数据
        /// </summary>
        /// <param name="currentMetadata">当前元数据</param>
        /// <returns>生成后的元数据</returns>
        public virtual Dictionary<string, object> GenerateMetadata(Dictionary<string, object> currentMetadata)
        {
            // 默认实现：不生成任何元数据，由子类决定
            return currentMetadata;
        }

        /// <summary>
        /// 获取脚本版本信息
        /// 子类可以重写以提供版本信息
        /// </summary>
        /// <returns>脚本版本</returns>
        protected virtual string GetScriptVersion()
        {
            return "1.0.0";
        }

        /// <summary>
        /// 获取参数数量
        /// </summary>
        /// <returns>参数数量</returns>
        protected virtual int GetParameterCount()
        {
            var scriptType = GetType();
            var properties = scriptType.GetProperties()
                .Where(p => p.GetCustomAttribute<ScriptParameterAttribute>() != null);
            return properties.Count();
        }

        /// <summary>
        /// 获取脚本特定的元数据
        /// 子类可以重写以提供特定的元数据
        /// </summary>
        /// <returns>脚本特定元数据</returns>
        protected virtual Dictionary<string, object> GetScriptSpecificMetadata()
        {
            return new Dictionary<string, object>();
        }

        /// <summary>
        /// 元数据提取完成回调
        /// 子类可以重写以响应元数据提取
        /// </summary>
        /// <param name="key">元数据键</param>
        /// <param name="value">元数据值</param>
        protected virtual void OnMetadataExtracted(string key, object value)
        {
            // 默认不做任何处理
        }

        /// <summary>
        /// 脚本初始化
        /// </summary>
        public virtual void Initialize(IScriptContext context)
        {
            // 默认不执行任何操作
        }

        /// <summary>
        /// 脚本清理
        /// </summary>
        public virtual void Cleanup()
        {
            // 默认不执行任何操作
        }

        #endregion

        #region INotifyPropertyChanged实现

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        /// <summary>
        /// Call this method when a script parameter has been changed,
        /// typically from the script's UI control or ViewModel.
        /// This will notify external listeners (like the host application) and
        /// also call the script's own OnParameterChangedAsync method.
        /// </summary>
        /// <param name="parameterName">The name of the parameter that changed.</param>
        /// <param name="newValue">The new value of the parameter.</param>
        public void OnParameterChanged(string parameterName, object? newValue)
        {
            // Notify external listeners (e.g., the host/NodeEditorViewModel)
            ParameterExternallyChanged?.Invoke(this, new ScriptParameterChangedEventArgs(parameterName, newValue));

            // Call the script's own async handler for its internal logic.
            // It is the script's responsibility to correctly update its state based on this call.
            // Note: We don't have the old value here, so we pass null
            _ = this.OnParameterChangedAsync(parameterName, null, newValue);
        }
    }

    /// <summary>
    /// Script ViewModel基类，提供通用功能
    /// </summary>
    public abstract class ScriptViewModelBase : IScriptViewModel
    {
        private bool _isProcessing;
        private double _progress;
        private string _errorMessage = string.Empty;
        private bool _isValid = true;

        protected readonly SynchronizationContext _uiContext;
        protected readonly CancellationTokenSource _cancellationTokenSource;

        public IRevivalScript Script { get; }
        public int NodeId { get; set; }

        public bool IsProcessing
        {
            get => _isProcessing;
            protected set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanExecute));
                }
            }
        }

        public double Progress
        {
            get => _progress;
            protected set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            protected set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool IsValid
        {
            get => _isValid;
            protected set
            {
                if (_isValid != value)
                {
                    _isValid = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanExecute));
                }
            }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool CanExecute => IsValid && !IsProcessing;

        protected ScriptViewModelBase(IRevivalScript script)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 在UI线程上执行操作
        /// </summary>
        protected void RunOnUIThread(Action action)
        {
            if (_uiContext == SynchronizationContext.Current)
            {
                action();
            }
            else
            {
                _uiContext.Post(_ => action(), null);
            }
        }

        /// <summary>
        /// 在UI线程上异步执行操作
        /// </summary>
        protected Task RunOnUIThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (_uiContext == SynchronizationContext.Current)
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }
            else
            {
                _uiContext.Post(_ =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, null);
            }

            return tcs.Task;
        }

        public virtual async Task InitializeAsync()
        {
            try
            {
                await RunOnUIThreadAsync(() =>
                {
                    IsProcessing = false;
                    Progress = 0;
                    ErrorMessage = string.Empty;
                    IsValid = true;
                });

                // 子类可重写此方法进行自定义初始化
                await OnInitializeAsync();
            }
            catch (Exception ex)
            {
                await RunOnUIThreadAsync(() =>
                {
                    ErrorMessage = $"初始化失败: {ex.Message}";
                    IsValid = false;
                });
            }
        }

        protected virtual Task OnInitializeAsync() => Task.CompletedTask;

        /// <summary>
        /// 鲁棒的参数变化处理 - 带防抖动和错误处理
        /// </summary>
        protected async Task HandleParameterChangeAsync(string parameterName, object oldValue, object newValue)
        {
            try
            {
                // 验证参数
                var validationResult = ValidateParameter(parameterName, newValue);
                if (!validationResult.IsValid)
                {
                    await RunOnUIThreadAsync(() =>
                    {
                        ErrorMessage = validationResult.ErrorMessage ?? "参数验证失败";
                        // 这里可以实现参数回滚逻辑
                    });
                    return;
                }

                // 清除错误信息
                await RunOnUIThreadAsync(() => ErrorMessage = string.Empty);

                // 调用子类的参数变化处理
                await OnParameterChangedAsync(parameterName, oldValue, newValue);

                // 移除多链路通知，只保留 OnParameterChanged 方法中的 ParameterExternallyChanged 事件
                // 这样确保只有单一链路：RevivalScriptBase.ParameterExternallyChanged → NodeEditorViewModel.HandleScriptParameterExternallyChanged
            }
            catch (Exception ex)
            {
                await RunOnUIThreadAsync(() =>
                {
                    ErrorMessage = $"参数处理失败: {ex.Message}";
                });
            }
        }

        // 移除 NotifyParameterChanged 方法
        // 不再需要通过复杂的链路通知参数变化
        // 改为使用单一链路：OnParameterChanged → ParameterExternallyChanged 事件

        public abstract Task OnParameterChangedAsync(string parameterName, object oldValue, object newValue);
        public abstract ScriptValidationResult ValidateParameter(string parameterName, object value);
        public abstract Dictionary<string, object> GetParameterData();
        public abstract Task SetParameterDataAsync(Dictionary<string, object> data);
        public abstract Task ResetToDefaultAsync();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
