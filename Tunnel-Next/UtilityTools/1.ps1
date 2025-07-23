# 批量替换API调用的PowerShell脚本
$rootPath = "Tunnel-Next/UtilityTools/BatchProcessor"
$targetExtensions = @("*.cs")

# 定义替换规则
$replacements = @(
    # 从AddLog到LogMessage的替换
    @{
        Old = "context\.AddLog\((.*?)\);";
        New = "context.LogMessage($1);";
    },
    # 从CreateChildContext到手动创建上下文的替换
    @{
        Old = "var childContext = context\.CreateChildContext\(\);";
        New = "// 创建新上下文，复制环境变量
var childContext = new ExecutionContext();
foreach (var kvp in context.Environment)
{
    childContext.Environment[kvp.Key] = kvp.Value;
}";
    },
    # 从SetProgress到直接设置属性的替换
    @{
        Old = "context\.SetProgress\((.*?), (.*?)\);";
        New = "context.Status = $2;
context.Progress = $1;";
    },
    # 临时目录路径替换
    @{
        Old = "context\.TempDirectory";
        New = "System.IO.Path.Combine(System.IO.Path.GetTempPath(), \"TunnelBatchProcessor_\" + DateTime.Now.Ticks)";
    },
    # TempFiles替换为Environment字典
    @{
        Old = "context\.TempFiles\[(.*?)\] = (.*?);";
        New = "context.Environment[\`$\"TempFile_{$1}\"] = $2;";
    },
    # TempFiles访问替换
    @{
        Old = "context\.TempFiles\[(.*?)\]";
        New = "context.Environment[\`$\"TempFile_{$1}\"]";
    },
    # ExecutionStatus枚举引用替换
    @{
        Old = "ExecutionStatus\.Running";
        New = "\"运行中\"";
    },
    @{
        Old = "ExecutionStatus\.Completed";
        New = "\"已完成\"";
    },
    @{
        Old = "ExecutionStatus\.Failed";
        New = "\"失败\"";
    }
)

# 查找所有目标文件
$files = Get-ChildItem -Path $rootPath -Include $targetExtensions -Recurse

# 执行替换
foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    $modified = $false

    foreach ($replacement in $replacements) {
        $newContent = $content -replace $replacement.Old, $replacement.New
        if ($newContent -ne $content) {
            $content = $newContent
            $modified = $true
        }
    }

    # 如果文件被修改，则保存更改
    if ($modified) {
        Write-Host "修改文件: $($file.FullName)"
        Set-Content -Path $file.FullName -Value $content
    }
}

Write-Host "替换完成!"