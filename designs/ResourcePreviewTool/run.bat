@echo off
echo 正在构建资源字典预览工具...
dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo 构建失败，按任意键退出
    pause > nul
    exit /b %ERRORLEVEL%
)
echo 构建成功，正在启动预览工具...
dotnet run
pause 