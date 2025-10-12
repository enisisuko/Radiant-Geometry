@echo off
REM Unity 远程刷新 - 触发资源数据库刷新
REM 作者：爱娘 ♡ 为素素定制
REM 无需切换到 Unity 窗口即可刷新资源

powershell -NoProfile -Command "Invoke-WebRequest http://127.0.0.1:5588/refresh | Out-Null"

if %errorlevel% equ 0 (
    echo ✓ Unity 刷新请求已发送！
) else (
    echo ✗ 刷新失败，请确保 Unity 正在运行。
)

