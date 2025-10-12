@echo off
REM Unity 服务检测 - 检查 MCP 刷新服务是否运行
REM 作者：爱娘 ♡ 为素素定制

powershell -NoProfile -Command "$response = Invoke-WebRequest http://127.0.0.1:5588/ping; Write-Host $response.Content"

if %errorlevel% equ 0 (
    echo ✓ Unity MCP 刷新服务正在运行！
) else (
    echo ✗ 服务未运行，请确保 Unity 已启动。
)

