using System;
using System.Threading.Tasks;
using Tunnel_Next.Services.ImageProcessing;
using OpenCvSharp;

namespace Tunnel_Next.Tests
{
    /// <summary>
    /// 对象池系统测试类
    /// </summary>
    public class ObjectPoolTest
    {
        public static async Task RunBasicTest()
        {
            Console.WriteLine("=== 对象池基础功能测试 ===");
            
            using var objectPool = new ImageProcessingObjectPool();
            
            // 测试Mat对象池
            await TestMatPool(objectPool);
            
            // 显示统计信息
            ShowStats(objectPool);
            
            Console.WriteLine("测试完成！");
        }
        
        private static async Task TestMatPool(ImageProcessingObjectPool objectPool)
        {
            Console.WriteLine("\n--- Mat对象池测试 ---");
            
            var size = new Size(1920, 1080);
            var matType = MatType.CV_8UC3;
            
            // 第一轮：创建新对象
            Console.WriteLine("第一轮：创建10个Mat对象");
            var mats = new Mat[10];
            for (int i = 0; i < 10; i++)
            {
                mats[i] = objectPool.GetMat(size, matType);
                Console.WriteLine($"  Mat {i}: {mats[i].Width}x{mats[i].Height}");
            }
            
            // 返回到池中
            Console.WriteLine("返回所有Mat到池中");
            for (int i = 0; i < 10; i++)
            {
                objectPool.ReturnMat(mats[i]);
            }
            
            // 第二轮：应该从池中复用
            Console.WriteLine("第二轮：获取5个Mat对象（应该复用）");
            var reusedMats = new Mat[5];
            for (int i = 0; i < 5; i++)
            {
                reusedMats[i] = objectPool.GetMat(size, matType);
                Console.WriteLine($"  复用Mat {i}: {reusedMats[i].Width}x{reusedMats[i].Height}");
            }
            
            // 返回复用的Mat
            for (int i = 0; i < 5; i++)
            {
                objectPool.ReturnMat(reusedMats[i]);
            }
            
            await Task.Delay(100); // 模拟异步操作
        }
        
        private static void ShowStats(ImageProcessingObjectPool objectPool)
        {
            Console.WriteLine("\n--- 统计信息 ---");
            var stats = objectPool.GetStats();
            
            Console.WriteLine($"Mat分配总数: {stats.TotalMatAllocations}");
            Console.WriteLine($"Mat复用总数: {stats.TotalMatReuses}");
            Console.WriteLine($"Mat复用率: {stats.MatReuseRate:P2}");
            Console.WriteLine($"当前Mat池大小: {stats.MatPoolSize}");
            Console.WriteLine($"Mat池类型数: {stats.MatPoolCount}");
            
            Console.WriteLine($"\n统计摘要: {stats}");
        }
        
        public static async Task RunPerformanceTest()
        {
            Console.WriteLine("\n=== 性能对比测试 ===");
            
            const int iterations = 1000;
            var size = new Size(1920, 1080);
            var matType = MatType.CV_8UC3;
            
            // 测试不使用对象池的性能
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                using var mat = new Mat(size, matType);
                mat.SetTo(0); // 模拟使用
            }
            sw.Stop();
            var timeWithoutPool = sw.ElapsedMilliseconds;
            
            // 测试使用对象池的性能
            using var objectPool = new ImageProcessingObjectPool();
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var mat = objectPool.GetMat(size, matType);
                mat.SetTo(0); // 模拟使用
                objectPool.ReturnMat(mat);
            }
            sw.Stop();
            var timeWithPool = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"创建{iterations}个Mat对象:");
            Console.WriteLine($"  不使用对象池: {timeWithoutPool}ms");
            Console.WriteLine($"  使用对象池: {timeWithPool}ms");
            Console.WriteLine($"  性能提升: {(double)timeWithoutPool / timeWithPool:F2}x");
            
            var stats = objectPool.GetStats();
            Console.WriteLine($"  复用率: {stats.MatReuseRate:P2}");
        }
        
        public static async Task RunLifecycleTest()
        {
            Console.WriteLine("\n=== 生命周期管理测试 ===");
            
            using var objectPool = new ImageProcessingObjectPool();
            
            // 创建一些资源
            var mat1 = objectPool.GetMat(new Size(800, 600), MatType.CV_8UC3);
            var mat2 = objectPool.GetMat(new Size(1280, 720), MatType.CV_16UC1);
            
            Console.WriteLine("创建了2个Mat对象");
            ShowStats(objectPool);
            
            // 返回资源
            objectPool.ReturnMat(mat1);
            objectPool.ReturnMat(mat2);
            
            Console.WriteLine("返回资源到池中");
            ShowStats(objectPool);
            
            // 手动触发维护
            Console.WriteLine("触发手动维护");
            objectPool.TriggerMaintenance();
            
            await Task.Delay(1000);
            ShowStats(objectPool);
        }
        
        public static async Task RunStressTest()
        {
            Console.WriteLine("\n=== 压力测试 ===");
            
            using var objectPool = new ImageProcessingObjectPool();
            var random = new Random();
            
            // 模拟随机的资源获取和释放
            var tasks = new Task[10];
            for (int t = 0; t < 10; t++)
            {
                int taskId = t;
                tasks[t] = Task.Run(async () =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var width = 512 + random.Next(1024);
                        var height = 512 + random.Next(1024);
                        var size = new Size(width, height);
                        var matType = random.Next(2) == 0 ? MatType.CV_8UC3 : MatType.CV_16UC1;
                        
                        var mat = objectPool.GetMat(size, matType);
                        
                        // 模拟使用
                        await Task.Delay(random.Next(10));
                        mat.SetTo(random.Next(256));
                        
                        objectPool.ReturnMat(mat);
                        
                        if (i % 20 == 0)
                        {
                            Console.WriteLine($"任务{taskId}: 完成{i}次操作");
                        }
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            
            Console.WriteLine("压力测试完成");
            ShowStats(objectPool);
            
            // 触发维护清理
            objectPool.TriggerMaintenance();
            await Task.Delay(1000);
            
            Console.WriteLine("维护后统计:");
            ShowStats(objectPool);
        }
    }
    
    /// <summary>
    /// 测试程序入口
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                await ObjectPoolTest.RunBasicTest();
                await ObjectPoolTest.RunPerformanceTest();
                await ObjectPoolTest.RunLifecycleTest();
                await ObjectPoolTest.RunStressTest();
                
                Console.WriteLine("\n所有测试完成！按任意键退出...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试过程中发生异常: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}
