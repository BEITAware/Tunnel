using System.Diagnostics;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// 描述图像处理所需的全局环境信息，可按需扩展。
    /// 目前仅包含节点图名称，后续可加入工作目录、批处理ID等。
    /// </summary>
    public class ProcessorEnvironment
    {
        /// <summary>
        /// 节点图名称
        /// </summary>
        public string NodeGraphName { get; set; } = string.Empty;

        /// <summary>
        /// 序号（可用于批处理编号或任务计数）
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// 环境字典，可用于注入任意元数据
        /// </summary>
        public Dictionary<string, object> EnvironmentDictionary { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 创建环境
        /// </summary>
        /// <param name="nodeGraphName">节点图名称</param>
        /// <param name="index">序号（可选，默认0）</param>
        /// <param name="environmentDictionary">环境字典（可选）</param>
        public ProcessorEnvironment(string nodeGraphName = "", int index = 0, Dictionary<string, object>? environmentDictionary = null)
        {
            NodeGraphName = nodeGraphName ?? string.Empty;
            Index = index;
            EnvironmentDictionary = environmentDictionary ?? new Dictionary<string, object>();
#if DEBUG
            Debug.WriteLine($"[ProcessorEnvironment] 创建 NodeGraphName=\"{NodeGraphName}\", Index={Index} at {new StackFrame(1, true).GetMethod()?.DeclaringType?.FullName}:{new StackFrame(1, true).GetFileLineNumber()}");
#endif
        }
    }
} 