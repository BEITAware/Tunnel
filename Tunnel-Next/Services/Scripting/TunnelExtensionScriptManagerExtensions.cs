using System;
using System.IO;
using Tunnel_Next.Services.Scripting;
using System.Collections.Generic;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// 兼容旧代码的扩展方法，为 ITunnelExtensionScript 提供扩展方法。
    /// </summary>
    public static class TunnelExtensionScriptManagerExtensions
    {
        /// <summary>
        /// 兼容旧版的 CreateScript 方法，内部调用 CreateTunnelExtensionScriptInstance。
        /// </summary>
        public static ITunnelExtensionScript? CreateScript(this TunnelExtensionScriptManager manager, string path)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            return manager.CreateTunnelExtensionScriptInstance(path);
        }

        /// <summary>
        /// 兼容旧版的 LoadScriptFromPath 方法，内部调用 CreateTunnelExtensionScriptInstance。
        /// </summary>
        public static ITunnelExtensionScript? LoadScriptFromPath(this TunnelExtensionScriptManager manager, string path)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            return manager.CreateTunnelExtensionScriptInstance(path);
        }

        /// <summary>
        /// 获取脚本路径
        /// </summary>
        public static string ScriptPath(this ITunnelExtensionScript script)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            
            // 尝试通过反射获取脚本路径
            var type = script.GetType();
            var pathProperty = type.GetProperty("ScriptPath");
            
            if (pathProperty != null)
            {
                return pathProperty.GetValue(script)?.ToString() ?? string.Empty;
            }
            
            return string.Empty;
        }

        /// <summary>
        /// 获取脚本名称
        /// </summary>
        public static string ScriptName(this ITunnelExtensionScript script)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            
            // 尝试通过反射获取脚本名称
            var type = script.GetType();
            var nameProperty = type.GetProperty("ScriptName");
            
            if (nameProperty != null)
            {
                return nameProperty.GetValue(script)?.ToString() ?? Path.GetFileNameWithoutExtension(ScriptPath(script));
            }
            
            // 如果获取不到名称，则使用文件名
            return Path.GetFileNameWithoutExtension(ScriptPath(script));
        }
        
        /// <summary>
        /// 获取脚本数据
        /// </summary>
        public static string ScriptData(this ITunnelExtensionScript script, Dictionary<string, object>? data = null)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            
            if (data != null)
            {
                // 将字典序列化为JSON字符串
                try
                {
                    return System.Text.Json.JsonSerializer.Serialize(data);
                }
                catch
                {
                    return string.Empty;
                }
            }
            
            return string.Empty;
        }
    }
} 