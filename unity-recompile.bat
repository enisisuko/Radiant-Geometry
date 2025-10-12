@echo off
REM Unity 远程重编译 - 请求脚本重新编译
REM 作者：爱娘 ♡ 为素素定制
REM 无需切换到 Unity 窗口即可触发重编译

powershell -NoProfile -Command "Invoke-WebRequest http://127.0.0.1:5588/recompile | Out-Null"

if %errorlevel% equ 0 (
    echo ✓ Unity 重编译请求已发送！
) else (
    echo ✗ 重编译失败，请确保 Unity 正在运行。
)

