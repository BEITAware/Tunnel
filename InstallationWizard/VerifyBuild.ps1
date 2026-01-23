# Tunnel 安装向导构建验证脚本
# 用于验证模板文件是否正确复制到构建输出目录

param(
    [string]$BuildOutputPath = "bin\Release\net8.0-windows\win-x64"
)

Write-Host "=== Tunnel 安装向导构建验证 ===" -ForegroundColor Cyan
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $scriptDir $BuildOutputPath

# 检查构建输出目录是否存在
if (-not (Test-Path $outputDir)) {
    Write-Host "错误: 构建输出目录不存在: $outputDir" -ForegroundColor Red
    Write-Host "请先构建项目" -ForegroundColor Yellow
    exit 1
}

Write-Host "构建输出目录: $outputDir" -ForegroundColor Green
Write-Host ""

# 验证关键目录和文件
$checks = @(
    @{
        Path = "Resources\Templates"
        Type = "Directory"
        Description = "模板目录"
    },
    @{
        Path = "Resources\Templates\空的节点图"
        Type = "Directory"
        Description = "空的节点图模板"
    },
    @{
        Path = "Resources\Templates\空的节点图\空的节点图.nodegraph"
        Type = "File"
        Description = "空的节点图文件"
    },
    @{
        Path = "Scripts"
        Type = "Directory"
        Description = "脚本目录"
    },
    @{
        Path = "App"
        Type = "Directory"
        Description = "应用程序目录"
    }
)

$allPassed = $true

foreach ($check in $checks) {
    $fullPath = Join-Path $outputDir $check.Path
    $exists = Test-Path $fullPath
    
    if ($exists) {
        Write-Host "[✓] $($check.Description): " -NoNewline -ForegroundColor Green
        Write-Host $check.Path -ForegroundColor Gray
    } else {
        Write-Host "[✗] $($check.Description): " -NoNewline -ForegroundColor Red
        Write-Host $check.Path -ForegroundColor Gray
        $allPassed = $false
    }
}

Write-Host ""

# 统计模板数量
$templatesDir = Join-Path $outputDir "Resources\Templates"
if (Test-Path $templatesDir) {
    $templateFolders = Get-ChildItem -Path $templatesDir -Directory
    Write-Host "发现 $($templateFolders.Count) 个模板:" -ForegroundColor Cyan
    
    foreach ($folder in $templateFolders) {
        $nodeGraphFiles = Get-ChildItem -Path $folder.FullName -Filter "*.nodegraph"
        $thumbnailFile = Join-Path $folder.FullName "thumbnail.png"
        
        Write-Host "  - $($folder.Name)" -ForegroundColor White
        Write-Host "    节点图文件: $($nodeGraphFiles.Count)" -ForegroundColor Gray
        
        if (Test-Path $thumbnailFile) {
            Write-Host "    缩略图: 存在" -ForegroundColor Gray
        } else {
            Write-Host "    缩略图: 不存在" -ForegroundColor Yellow
        }
    }
}

Write-Host ""

if ($allPassed) {
    Write-Host "=== 验证通过 ===" -ForegroundColor Green
    exit 0
} else {
    Write-Host "=== 验证失败 ===" -ForegroundColor Red
    Write-Host "请检查项目配置，确保模板文件被正确复制" -ForegroundColor Yellow
    exit 1
}
