@echo off
echo 启动MCPControl服务器...
echo 服务器将在 http://localhost:3232 运行
echo 按 Ctrl+C 停止服务器
echo.
mcp-control --sse --port 3232
pause
