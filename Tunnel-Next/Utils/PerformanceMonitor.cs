using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Tunnel_Next.Utils
{
    /// <summary>
    /// 性能监控工具类
    /// </summary>
    public static class PerformanceMonitor
    {
        private static readonly Dictionary<string, Stopwatch> _timers = new();
        private static readonly Dictionary<string, List<long>> _measurements = new();
        private static readonly object _lock = new object();

        /// <summary>
        /// 开始计时
        /// </summary>
        public static void StartTimer(string name)
        {
            lock (_lock)
            {
                if (!_timers.ContainsKey(name))
                {
                    _timers[name] = new Stopwatch();
                }
                
                _timers[name].Restart();
            }
        }

        /// <summary>
        /// 停止计时并记录结果
        /// </summary>
        public static long StopTimer(string name)
        {
            lock (_lock)
            {
                if (_timers.TryGetValue(name, out var timer))
                {
                    timer.Stop();
                    var elapsed = timer.ElapsedMilliseconds;
                    
                    if (!_measurements.ContainsKey(name))
                    {
                        _measurements[name] = new List<long>();
                    }
                    
                    _measurements[name].Add(elapsed);
                    
                    // 保持最近100次测量
                    if (_measurements[name].Count > 100)
                    {
                        _measurements[name].RemoveAt(0);
                    }
                    
                    return elapsed;
                }
                
                return 0;
            }
        }

        /// <summary>
        /// 获取平均执行时间
        /// </summary>
        public static double GetAverageTime(string name)
        {
            lock (_lock)
            {
                if (_measurements.TryGetValue(name, out var measurements) && measurements.Count > 0)
                {
                    return measurements.Average();
                }
                
                return 0;
            }
        }

        /// <summary>
        /// 获取最大执行时间
        /// </summary>
        public static long GetMaxTime(string name)
        {
            lock (_lock)
            {
                if (_measurements.TryGetValue(name, out var measurements) && measurements.Count > 0)
                {
                    return measurements.Max();
                }
                
                return 0;
            }
        }

        /// <summary>
        /// 获取最小执行时间
        /// </summary>
        public static long GetMinTime(string name)
        {
            lock (_lock)
            {
                if (_measurements.TryGetValue(name, out var measurements) && measurements.Count > 0)
                {
                    return measurements.Min();
                }
                
                return 0;
            }
        }

        /// <summary>
        /// 获取性能报告
        /// </summary>
        public static string GetPerformanceReport()
        {
            lock (_lock)
            {
                var report = "=== 性能监控报告 ===\n";
                
                foreach (var kvp in _measurements)
                {
                    var name = kvp.Key;
                    var measurements = kvp.Value;
                    
                    if (measurements.Count > 0)
                    {
                        var avg = measurements.Average();
                        var min = measurements.Min();
                        var max = measurements.Max();
                        var count = measurements.Count;
                        
                        report += $"{name}:\n";
                        report += $"  次数: {count}\n";
                        report += $"  平均: {avg:F2}ms\n";
                        report += $"  最小: {min}ms\n";
                        report += $"  最大: {max}ms\n\n";
                    }
                }
                
                return report;
            }
        }

        /// <summary>
        /// 清除所有测量数据
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _timers.Clear();
                _measurements.Clear();
            }
        }

        /// <summary>
        /// 清除指定名称的测量数据
        /// </summary>
        public static void Clear(string name)
        {
            lock (_lock)
            {
                _timers.Remove(name);
                _measurements.Remove(name);
            }
        }

        /// <summary>
        /// 使用using语句的计时器
        /// </summary>
        public static IDisposable CreateTimer(string name)
        {
            return new TimerScope(name);
        }

        private class TimerScope : IDisposable
        {
            private readonly string _name;
            private bool _disposed = false;

            public TimerScope(string name)
            {
                _name = name;
                StartTimer(_name);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    var elapsed = StopTimer(_name);
                    _disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// 渲染性能统计
    /// </summary>
    public class RenderingStats
    {
        public int NodesRendered { get; set; }
        public int ConnectionsRendered { get; set; }
        public int NodesInViewport { get; set; }
        public int ConnectionsInViewport { get; set; }
        public long RenderTime { get; set; }
        public long UpdateTime { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public override string ToString()
        {
            return $"节点: {NodesRendered}/{NodesInViewport}, 连接: {ConnectionsRendered}/{ConnectionsInViewport}, " +
                   $"渲染: {RenderTime}ms, 更新: {UpdateTime}ms";
        }
    }
}
