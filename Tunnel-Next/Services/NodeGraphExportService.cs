using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using Tunnel_Next.Models;
using Tunnel_Next.Services.ImageProcessing;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 节点图导出服务 - 负责将节点图的F32bmp输出转换为JPEG并保存
    /// </summary>
    public class NodeGraphExportService
    {
        private readonly NodeGraphInterpreterService _interpreterService;
        private readonly WorkFolderService _workFolderService;

        public NodeGraphExportService(
            NodeGraphInterpreterService interpreterService,
            WorkFolderService workFolderService)
        {
            _interpreterService = interpreterService ?? throw new ArgumentNullException(nameof(interpreterService));
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
        }

        /// <summary>
        /// 导出节点图的F32bmp输出为JPEG文件
        /// </summary>
        /// <param name="resource">节点图资源对象</param>
        /// <param name="parameters">操作参数</param>
        /// <returns>操作结果</returns>
        public async Task<ResourceOperationResult> ExportNodeGraphAsync(ResourceObject resource, Dictionary<string, object>? parameters = null)
        {
            try
            {
                if (resource.ResourceType != ResourceItemType.NodeGraph)
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = "资源类型不是节点图"
                    };
                }

                if (!File.Exists(resource.FilePath))
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"节点图文件不存在: {resource.FilePath}"
                    };
                }

                // 创建处理环境
                var processorEnvironment = CreateProcessorEnvironment(resource, parameters);

                // 使用节点图解释器获取返回值
                var returnValues = await _interpreterService.InterpretNodeGraphAsync(resource.FilePath, processorEnvironment);
                if (returnValues == null || returnValues.Count == 0)
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = "节点图没有返回值或执行失败"
                    };
                }

                // 筛选F32bmp类型的输出
                var f32bmpOutputs = FilterF32bmpOutputs(returnValues);
                if (f32bmpOutputs.Count == 0)
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = "节点图没有F32bmp类型的输出"
                    };
                }

                // 确保Resources文件夹存在
                var resourcesFolder = _workFolderService.ResourcesFolder;
                Directory.CreateDirectory(resourcesFolder);

                // 转换并保存JPEG文件
                var savedFiles = new List<string>();
                var nodeGraphName = Path.GetFileNameWithoutExtension(resource.FilePath);

                foreach (var kvp in f32bmpOutputs)
                {
                    var portName = kvp.Key;
                    var mat = kvp.Value;

                    try
                    {
                        // 生成输出文件名
                        var fileName = f32bmpOutputs.Count == 1 
                            ? $"{nodeGraphName}.jpg" 
                            : $"{nodeGraphName}_{portName}.jpg";
                        var outputPath = Path.Combine(resourcesFolder, fileName);

                        // 转换为高质量JPEG
                        var success = ConvertMatToJpeg(mat, outputPath);
                        if (success)
                        {
                            savedFiles.Add(outputPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录单个文件转换失败，但继续处理其他文件
                        System.Diagnostics.Debug.WriteLine($"[NodeGraphExportService] 转换端口 {portName} 失败: {ex.Message}");
                    }
                }

                if (savedFiles.Count == 0)
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = "没有成功转换任何F32bmp输出"
                    };
                }

                return new ResourceOperationResult
                {
                    Success = true,
                    OutputPath = savedFiles.Count == 1 ? savedFiles[0] : resourcesFolder,
                    AdditionalData = 
                    {
                        ["SavedFiles"] = savedFiles,
                        ["SavedCount"] = savedFiles.Count
                    }
                };
            }
            catch (Exception ex)
            {
                return new ResourceOperationResult
                {
                    Success = false,
                    ErrorMessage = $"导出节点图失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 创建处理环境
        /// </summary>
        private ProcessorEnvironment CreateProcessorEnvironment(ResourceObject resource, Dictionary<string, object>? parameters)
        {
            var nodeGraphName = Path.GetFileNameWithoutExtension(resource.FilePath);
            var index = 0;
            var envDict = new Dictionary<string, object>();

            if (parameters != null)
            {
                // 从参数中提取信息
                if (parameters.TryGetValue("Index", out var indexValue) && indexValue is int idx)
                {
                    index = idx;
                }

                if (parameters.TryGetValue("Environment", out var envValue) && envValue is Dictionary<string, object> env)
                {
                    envDict = env;
                }
            }

            return new ProcessorEnvironment(nodeGraphName, index, envDict);
        }

        /// <summary>
        /// 筛选F32bmp类型的输出
        /// </summary>
        private Dictionary<string, Mat> FilterF32bmpOutputs(Dictionary<string, object> returnValues)
        {
            var f32bmpOutputs = new Dictionary<string, Mat>();

            foreach (var kvp in returnValues)
            {
                if (kvp.Value is Mat mat && !mat.IsDisposed && !mat.Empty())
                {
                    // 检查Mat是否为F32类型（32位浮点）
                    if (mat.Type() == MatType.CV_32FC3 || mat.Type() == MatType.CV_32FC4)
                    {
                        f32bmpOutputs[kvp.Key] = mat;
                    }
                }
            }

            return f32bmpOutputs;
        }

        /// <summary>
        /// 将Mat转换为高质量JPEG文件
        /// </summary>
        private bool ConvertMatToJpeg(Mat mat, string outputPath)
        {
            try
            {
                // 创建输出目录
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 转换为8位图像用于JPEG保存
                Mat outputMat;
                if (mat.Type() == MatType.CV_32FC3 || mat.Type() == MatType.CV_32FC4)
                {
                    // 32位浮点转换为8位
                    outputMat = new Mat();
                    mat.ConvertTo(outputMat, MatType.CV_8UC3, 255.0);
                }
                else
                {
                    outputMat = mat.Clone();
                }

                try
                {
                    // 设置高质量JPEG参数
                    var jpegParams = new int[] { (int)ImwriteFlags.JpegQuality, 100 };
                    
                    // 保存为JPEG
                    var success = Cv2.ImWrite(outputPath, outputMat, jpegParams);
                    
                    return success;
                }
                finally
                {
                    outputMat?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeGraphExportService] 转换Mat到JPEG失败: {ex.Message}");
                return false;
            }
        }
    }
}
