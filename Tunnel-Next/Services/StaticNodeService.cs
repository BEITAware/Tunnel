using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using Tunnel_Next.Models;
using OpenCvSharp;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 静态节点服务 - 负责将端口输出保存为静态节点文件
    /// </summary>
    public class StaticNodeService
    {
        private readonly WorkFolderService _workFolderService;

        public StaticNodeService(WorkFolderService workFolderService)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
        }

        /// <summary>
        /// 保存端口输出为静态节点
        /// </summary>
        /// <param name="node">源节点</param>
        /// <param name="portName">端口名称</param>
        /// <param name="value">端口输出值</param>
        /// <param name="customName">自定义名称（可选）</param>
        /// <returns>保存是否成功，以及保存的文件名</returns>
        public (bool success, string fileName) SaveAsStaticNode(Node node, string portName, object value, string customName = null)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (string.IsNullOrEmpty(portName))
                throw new ArgumentException("端口名称不能为空", nameof(portName));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                // 获取端口
                var port = node.GetOutputPort(portName);
                if (port == null)
                    throw new InvalidOperationException($"无法找到输出端口: {portName}");

                // 获取静态节点保存目录
                var staticNodesDir = GetStaticNodesDirectory();

                // 生成文件名
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName;
                
                // 如果提供了自定义名称，使用自定义名称，否则使用自动生成的名称
                if (!string.IsNullOrEmpty(customName))
                {
                    fileName = $"{SanitizeFileName(customName)}.tsn";
                }
                else
                {
                    // 使用默认命名规则（节点名称_端口名称_时间戳.tsn）
                    fileName = $"{SanitizeFileName(node.Title)}_{SanitizeFileName(portName)}_{timestamp}.tsn";
                }
                
                string filePath = Path.Combine(staticNodesDir, fileName);

                // 将端口输出包装到静态节点数据中
                var staticNodeData = new StaticNodeData
                {
                    OriginalNodeName = node.Title,
                    OriginalPortName = portName,
                    OriginalDataType = port.DataType.ToString(),
                    Data = ConvertToSerializableData(value)
                };

                // 写入文件
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    // 使用二进制序列化保存数据
                    using (var bw = new BinaryWriter(fs))
                    {
                        // 写入文件标识符和版本
                        byte[] header = Encoding.ASCII.GetBytes("TSN1"); // TSN文件格式，版本1
                        bw.Write(header.Length);
                        bw.Write(header);
                        
                        // 写入元数据
                        byte[] nodeNameBytes = Encoding.UTF8.GetBytes(staticNodeData.OriginalNodeName);
                        byte[] portNameBytes = Encoding.UTF8.GetBytes(staticNodeData.OriginalPortName);
                        byte[] dataTypeBytes = Encoding.UTF8.GetBytes(staticNodeData.OriginalDataType);
                        
                        bw.Write(nodeNameBytes.Length);
                        bw.Write(nodeNameBytes);
                        
                        bw.Write(portNameBytes.Length);
                        bw.Write(portNameBytes);
                        
                        bw.Write(dataTypeBytes.Length);
                        bw.Write(dataTypeBytes);
                        
                        // 写入数据
                        if (staticNodeData.Data is SerializableMat mat)
                        {
                            // 写入Mat数据类型标识
                            bw.Write("MAT");
                            
                            // 保存Mat基本信息
                            bw.Write(mat.Type);
                            bw.Write(mat.Rows);
                            bw.Write(mat.Cols);
                            bw.Write(mat.Channels);
                            
                            // 保存Mat数据
                            bw.Write(mat.Data.Length);
                            bw.Write(mat.Data);
                        }
                        else if (staticNodeData.Data is byte[] byteArray)
                        {
                            // 写入字节数组数据类型标识
                            bw.Write("BYTES");
                            
                            // 保存字节数组
                            bw.Write(byteArray.Length);
                            bw.Write(byteArray);
                        }
                        else if (staticNodeData.Data is double doubleValue)
                        {
                            bw.Write("DOUBLE");
                            bw.Write(doubleValue);
                        }
                        else if (staticNodeData.Data is float floatValue)
                        {
                            bw.Write("FLOAT");
                            bw.Write(floatValue);
                        }
                        else if (staticNodeData.Data is int intValue)
                        {
                            bw.Write("INT");
                            bw.Write(intValue);
                        }
                        else if (staticNodeData.Data is bool boolValue)
                        {
                            bw.Write("BOOL");
                            bw.Write(boolValue);
                        }
                        else if (staticNodeData.Data is string stringValue)
                        {
                            bw.Write("STRING");
                            byte[] stringBytes = Encoding.UTF8.GetBytes(stringValue);
                            bw.Write(stringBytes.Length);
                            bw.Write(stringBytes);
                        }
                        else
                        {
                            // 未知类型，保存为字符串表示
                            bw.Write("UNKNOWN");
                            string dataStr = staticNodeData.Data?.ToString() ?? "null";
                            byte[] dataBytes = Encoding.UTF8.GetBytes(dataStr);
                            bw.Write(dataBytes.Length);
                            bw.Write(dataBytes);
                        }
                    }
                }

                return (true, fileName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存静态节点失败: {ex.Message}");
                return (false, string.Empty);
            }
        }

        /// <summary>
        /// 将对象转换为可序列化的数据
        /// </summary>
        private object ConvertToSerializableData(object value)
        {
            // 处理特殊类型
            if (value is Mat mat)
            {
                return new SerializableMat(mat);
            }
            else if (value is byte[] || value is double || value is float || value is int || value is bool || value is string)
            {
                // 这些类型可以直接序列化
                return value;
            }
            else
            {
                // 其他类型尝试转换为字符串
                return value?.ToString() ?? "";
            }
        }

        /// <summary>
        /// 获取静态节点目录，如果不存在则创建
        /// </summary>
        private string GetStaticNodesDirectory()
        {
            var workFolder = _workFolderService.WorkFolder;
            var staticNodesDir = Path.Combine(workFolder, "Resources", "StaticNodes");
            
            if (!Directory.Exists(staticNodesDir))
            {
                Directory.CreateDirectory(staticNodesDir);
            }
            
            return staticNodesDir;
        }

        /// <summary>
        /// 清理文件名，移除不合法字符
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            return fileName;
        }
    }

    /// <summary>
    /// 静态节点数据结构
    /// </summary>
    [Serializable]
    public class StaticNodeData
    {
        public string OriginalNodeName { get; set; } = string.Empty;
        public string OriginalPortName { get; set; } = string.Empty;
        public string OriginalDataType { get; set; } = string.Empty;
        public object Data { get; set; } = null;
    }

    /// <summary>
    /// 可序列化的Mat封装类
    /// </summary>
    [Serializable]
    public class SerializableMat
    {
        public int Type { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public int Channels { get; set; }
        public byte[] Data { get; set; }

        public SerializableMat()
        {
            Data = Array.Empty<byte>();
        }

        public SerializableMat(Mat mat)
        {
            if (mat == null || mat.Empty())
            {
                Type = MatType.CV_32FC4.ToInt32();
                Rows = 1;
                Cols = 1;
                Channels = 4;
                Data = new byte[16]; // 1x1 32FC4 = 16 bytes
                return;
            }

            Type = mat.Type().ToInt32();
            Rows = mat.Rows;
            Cols = mat.Cols;
            Channels = mat.Channels();

            // 获取连续的Mat
            Mat continuousMat = mat;
            if (!mat.IsContinuous())
            {
                continuousMat = new Mat();
                mat.CopyTo(continuousMat);
            }

            // 复制数据
            long dataSize = continuousMat.Total() * continuousMat.ElemSize();
            if (dataSize > int.MaxValue)
            {
                throw new ArgumentException($"Mat太大，无法序列化: {dataSize} 字节");
            }

            Data = new byte[(int)dataSize];
            System.Runtime.InteropServices.Marshal.Copy(continuousMat.Data, Data, 0, Data.Length);

            // 如果创建了新的Mat，释放它
            if (continuousMat != mat)
            {
                continuousMat.Dispose();
            }
        }

        /// <summary>
        /// 转换回OpenCV的Mat
        /// </summary>
        public Mat ToMat()
        {
            try
            {
                // 创建Mat
                Mat mat = new Mat(Rows, Cols, (MatType)Type);
                
                // 复制数据
                System.Runtime.InteropServices.Marshal.Copy(Data, 0, mat.Data, Data.Length);
                
                return mat;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转换为Mat失败: {ex.Message}");
                
                // 返回一个红色的1x1 Mat作为错误指示
                Mat errorMat = new Mat(1, 1, MatType.CV_32FC4, new Scalar(1, 0, 0, 1));
                return errorMat;
            }
        }
    }
} 