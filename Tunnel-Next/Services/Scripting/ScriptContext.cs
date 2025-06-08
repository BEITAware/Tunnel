using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Windows;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// 脚本上下文实现 - 为脚本提供应用程序访问接口
    /// </summary>
    public class ScriptContext : IScriptContext
    {
        private readonly Func<NodeGraph> _getCurrentNodeGraph;
        private readonly Action<List<int>> _processNodeGraph;
        private readonly Func<int, Dictionary<string, object>> _getNodeInputs;
        private readonly Action<int, string, object> _updateNodeParameter;

        public string WorkFolder { get; }
        public string TempFolder { get; }
        public string ScriptsFolder { get; }
        public string? CurrentImagePath { get; set; }
        public double ZoomLevel { get; set; } = 1.0;

        public ScriptContext(
            string workFolder,
            string tempFolder,
            string scriptsFolder,
            Func<NodeGraph> getCurrentNodeGraph,
            Action<List<int>> processNodeGraph,
            Func<int, Dictionary<string, object>> getNodeInputs,
            Action<int, string, object> updateNodeParameter)
        {
            WorkFolder = workFolder;
            TempFolder = tempFolder;
            ScriptsFolder = scriptsFolder;
            _getCurrentNodeGraph = getCurrentNodeGraph;
            _processNodeGraph = processNodeGraph;
            _getNodeInputs = getNodeInputs;
            _updateNodeParameter = updateNodeParameter;
        }

        public Dictionary<string, object> GetNodeInputs(int nodeId)
        {
            try
            {
                return _getNodeInputs(nodeId);
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>();
            }
        }

        public void UpdateNodeParameter(int nodeId, string paramName, object value)
        {
            try
            {
                _updateNodeParameter(nodeId, paramName, value);
            }
            catch (Exception ex)
            {
            }
        }

        public void ProcessNodeGraph(List<int> changedNodeIds)
        {
            try
            {
                _processNodeGraph(changedNodeIds);
            }
            catch (Exception ex)
            {
            }
        }

        public void SetNodeOutputs(int nodeId, Dictionary<string, object> outputs)
        {
            // 新的Revival Scripts接口方法，暂时空实现
            // 后续需要实现具体的节点输出设置逻辑
        }

        public T? GetService<T>() where T : class
        {
            // 新的Revival Scripts接口方法，暂时返回null
            // 后续需要实现服务定位器模式
            return null;
        }

        public void ShowMessage(string message, string title = "信息")
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
            }
        }

        public string? ShowFileDialog(string filter = "所有文件 (*.*)|*.*", string title = "选择文件")
        {
            try
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new OpenFileDialog
                    {
                        Filter = filter,
                        Title = title
                    };

                    return dialog.ShowDialog() == true ? dialog.FileName : null;
                });
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public string? ShowSaveDialog(string filter = "所有文件 (*.*)|*.*", string title = "保存文件")
        {
            try
            {
                return Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new SaveFileDialog
                    {
                        Filter = filter,
                        Title = title
                    };

                    return dialog.ShowDialog() == true ? dialog.FileName : null;
                });
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
