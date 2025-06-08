using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// Revival Script特性 - 定义脚本的基本信息
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RevivalScriptAttribute : Attribute
    {
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public string Category { get; set; } = "通用";
        public string Color { get; set; } = "#4A90E2";
    }

    // 移除了过时的NodeScriptAttribute，现在只使用RevivalScriptAttribute

    /// <summary>
    /// 脚本参数特性 - 定义Revival Script参数
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ScriptParameterAttribute : Attribute
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
    }

    /// <summary>
    /// 节点参数特性 - 定义节点参数（保留向后兼容）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    [Obsolete("请使用ScriptParameterAttribute替代")]
    public class NodeParameterAttribute : Attribute
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ParameterType Type { get; set; } = ParameterType.Text;
        public double MinValue { get; set; } = double.MinValue;
        public double MaxValue { get; set; } = double.MaxValue;
        public double Step { get; set; } = 1.0;
        public string[] Options { get; set; } = Array.Empty<string>();
        public string FileFilter { get; set; } = "所有文件 (*.*)|*.*";
        public int Order { get; set; } = 0;
    }

    /// <summary>
    /// 参数类型枚举
    /// </summary>
    public enum ParameterType
    {
        Text,           // 文本输入
        Number,         // 数值输入
        Slider,         // 滑块
        Checkbox,       // 复选框
        Dropdown,       // 下拉列表
        FilePath,       // 文件路径选择
        FolderPath,     // 文件夹路径选择
        Color,          // 颜色选择器
        Range           // 范围选择器
    }

    /// <summary>
    /// 端口定义类
    /// </summary>
    public class PortDefinition
    {
        public string DataType { get; set; } = string.Empty;
        public bool IsFlexible { get; set; } = false;
        public string Description { get; set; } = string.Empty;

        public PortDefinition() { }

        public PortDefinition(string dataType, bool isFlexible = false, string description = "")
        {
            DataType = dataType;
            IsFlexible = isFlexible;
            Description = description;
        }
    }

    /// <summary>
    /// Revival Script接口 - 新的脚本系统接口
    /// </summary>
    public interface IRevivalScript
    {
        /// <summary>
        /// 获取输入端口定义
        /// </summary>
        Dictionary<string, PortDefinition> GetInputPorts();

        /// <summary>
        /// 获取输出端口定义
        /// </summary>
        Dictionary<string, PortDefinition> GetOutputPorts();

        /// <summary>
        /// 核心处理函数
        /// </summary>
        Dictionary<string, object> Process(Dictionary<string, object> inputs, IScriptContext context);

        /// <summary>
        /// 创建WPF参数控件
        /// </summary>
        FrameworkElement CreateParameterControl();

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        IScriptViewModel CreateViewModel();

        /// <summary>
        /// 参数变化通知（异步处理）
        /// </summary>
        Task OnParameterChangedAsync(string parameterName, object oldValue, object newValue);

        /// <summary>
        /// 序列化参数 - 节点负责自己的参数序列化
        /// </summary>
        /// <returns>序列化后的参数字典，键为参数名，值为序列化后的数据</returns>
        Dictionary<string, object> SerializeParameters();

        /// <summary>
        /// 反序列化参数 - 节点负责自己的参数反序列化和应用
        /// </summary>
        /// <param name="data">参数数据字典，键为参数名，值为序列化的数据</param>
        void DeserializeParameters(Dictionary<string, object> data);

        /// <summary>
        /// 注入元数据到下游（不覆盖已有键）
        /// </summary>
        Dictionary<string, object> InjectMetadata(Dictionary<string, object> currentMetadata)
        {
            return currentMetadata; // 默认不注入
        }

        /// <summary>
        /// 从上游提取所需元数据
        /// </summary>
        void ExtractMetadata(Dictionary<string, object> upstreamMetadata)
        {
            // 默认不提取
        }

        /// <summary>
        /// 强制覆盖元数据键值对（可选）
        /// </summary>
        Dictionary<string, object> GenerateMetadata(Dictionary<string, object> currentMetadata)
        {
            return currentMetadata; // 默认不生成
        }

        /// <summary>
        /// 脚本初始化（可选）
        /// </summary>
        void Initialize(IScriptContext context) { }

        /// <summary>
        /// 脚本清理（可选）
        /// </summary>
        void Cleanup() { }
    }

    // 移除了过时的INodeScript接口，现在只使用IRevivalScript

    /// <summary>
    /// 节点参数定义
    /// </summary>
    public class NodeParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public ParameterType Type { get; set; } = ParameterType.Text;
        public object DefaultValue { get; set; } = null!;
        public double MinValue { get; set; } = double.MinValue;
        public double MaxValue { get; set; } = double.MaxValue;
        public double Step { get; set; } = 1.0;
        public string[] Options { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = string.Empty;
        public string FileFilter { get; set; } = "所有文件 (*.*)|*.*";
        public int Order { get; set; } = 0;
    }

    /// <summary>
    /// 脚本ViewModel接口
    /// </summary>
    public interface IScriptViewModel : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// 脚本实例引用
        /// </summary>
        IRevivalScript Script { get; }

        /// <summary>
        /// 节点ID（用于标识）
        /// </summary>
        int NodeId { get; set; }

        /// <summary>
        /// 是否正在处理中
        /// </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// 处理进度（0-100）
        /// </summary>
        double Progress { get; }

        /// <summary>
        /// 错误信息
        /// </summary>
        string ErrorMessage { get; }

        /// <summary>
        /// 参数验证状态
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 初始化ViewModel
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 参数变化通知
        /// </summary>
        Task OnParameterChangedAsync(string parameterName, object oldValue, object newValue);

        /// <summary>
        /// 验证参数
        /// </summary>
        ScriptValidationResult ValidateParameter(string parameterName, object value);

        /// <summary>
        /// 获取所有参数数据
        /// </summary>
        Dictionary<string, object> GetParameterData();

        /// <summary>
        /// 设置参数数据
        /// </summary>
        Task SetParameterDataAsync(Dictionary<string, object> data);

        /// <summary>
        /// 重置到默认值
        /// </summary>
        Task ResetToDefaultAsync();
    }

    /// <summary>
    /// 脚本验证结果类
    /// </summary>
    public class ScriptValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        public ScriptValidationResult(bool isValid, string? errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// 脚本上下文接口 - 提供给脚本的应用程序上下文
    /// </summary>
    public interface IScriptContext
    {
        /// <summary>
        /// 工作文件夹路径
        /// </summary>
        string WorkFolder { get; }

        /// <summary>
        /// 临时文件夹路径
        /// </summary>
        string TempFolder { get; }

        /// <summary>
        /// 脚本文件夹路径
        /// </summary>
        string ScriptsFolder { get; }

        /// <summary>
        /// 当前图像路径
        /// </summary>
        string? CurrentImagePath { get; }

        /// <summary>
        /// 缩放级别
        /// </summary>
        double ZoomLevel { get; }

        /// <summary>
        /// 获取节点输入数据
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>节点输入数据</returns>
        Dictionary<string, object> GetNodeInputs(int nodeId);

        /// <summary>
        /// 设置节点输出数据
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <param name="outputs">输出数据</param>
        void SetNodeOutputs(int nodeId, Dictionary<string, object> outputs);

        /// <summary>
        /// 获取服务
        /// </summary>
        T? GetService<T>() where T : class;

        /// <summary>
        /// 更新节点参数（保留向后兼容）
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <param name="paramName">参数名</param>
        /// <param name="value">新值</param>
        [Obsolete("请使用SetNodeOutputs替代")]
        void UpdateNodeParameter(int nodeId, string paramName, object value) { }

        /// <summary>
        /// 触发节点图重新处理（保留向后兼容）
        /// </summary>
        /// <param name="changedNodeIds">变更的节点ID列表</param>
        [Obsolete("请使用新的事件机制")]
        void ProcessNodeGraph(List<int> changedNodeIds) { }

        /// <summary>
        /// 显示消息框
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        void ShowMessage(string message, string title = "信息");

        /// <summary>
        /// 显示文件选择对话框
        /// </summary>
        /// <param name="filter">文件过滤器</param>
        /// <param name="title">对话框标题</param>
        /// <returns>选择的文件路径，取消则返回null</returns>
        string? ShowFileDialog(string filter = "所有文件 (*.*)|*.*", string title = "选择文件");

        /// <summary>
        /// 显示文件保存对话框
        /// </summary>
        /// <param name="filter">文件过滤器</param>
        /// <param name="title">对话框标题</param>
        /// <returns>保存的文件路径，取消则返回null</returns>
        string? ShowSaveDialog(string filter = "所有文件 (*.*)|*.*", string title = "保存文件");
    }

    // 移除了过时的ScriptInfo类，现在只使用RevivalScriptInfo

    // 移除了过时的ScriptCompilationResult类，现在只使用RevivalScriptCompilationResult
}
