using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// SimpleTunnelExtensionScriptBase 提供对 TunnelExtensionScriptBase 的简化封装，
    /// 默认实现了参数序列化 / 反序列化、空参数面板、
    /// 以及一个最小可用的 ViewModel，方便编写纯算法脚本。
    /// </summary>
    public abstract class SimpleTunnelExtensionScriptBase : TunnelExtensionScriptBase
    {
        #region 参数序列化/反序列化
        public override Dictionary<string, object> SerializeParameters() => new();
        public override void DeserializeParameters(Dictionary<string, object> data) { }
        #endregion

        #region 参数 UI 与 ViewModel
        public override FrameworkElement CreateParameterControl() => new TextBlock { Text = "无可设置参数" };

        private class NoOpViewModel : ScriptViewModelBase
        {
            public NoOpViewModel(ITunnelExtensionScript script) : base(script) { }
            public override Task OnParameterChangedAsync(string parameterName, object oldValue, object newValue) => Task.CompletedTask;
            public override ScriptValidationResult ValidateParameter(string parameterName, object value) => new(true);
            public override Dictionary<string, object> GetParameterData() => new();
            public override Task SetParameterDataAsync(Dictionary<string, object> data) => Task.CompletedTask;
            public override Task ResetToDefaultAsync() => Task.CompletedTask;
        }

        public override IScriptViewModel CreateViewModel() => new NoOpViewModel(this);
        #endregion

        #region 参数变更处理
        public override Task OnParameterChangedAsync(string parameterName, object oldValue, object newValue) => Task.CompletedTask;
        #endregion
    }
} 