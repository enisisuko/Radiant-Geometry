@echo off
REM MCP 刷新服务器 - URL ACL 配置脚本
REM 需要以管理员权限运行
REM 作者：爱娘 ♡ 为素素定制

echo ========================================
echo  MCP 刷新服务器 - URL ACL 配置
echo ========================================
echo.

echo 正在配置 URL ACL 权限...
netsh http add urlacl url=http://127.0.0.1:5588/ user=Everyone

if %errorlevel% equ 0 (
    echo.
    echo ✓ 配置成功！
    echo.
    echo 现在可以在 Unity 中使用 MCP 刷新服务器了～
    echo.
) else (
    echo.
    echo ✗ 配置失败！请确保以管理员权限运行此脚本。
    echo.
)

pause

