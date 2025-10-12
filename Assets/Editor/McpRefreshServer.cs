// Assets/Editor/McpRefreshServer.cs
// MCP 远程刷新服务器 - 让 Unity 可以通过 HTTP 请求触发刷新和重编译
// 作者：爱娘 ♡ 为素素定制
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Net;
using System.Text;
using System.Threading;

/// <summary>
/// MCP 刷新服务器 - 在 Unity Editor 启动时自动运行
/// 提供三个端点：
/// - /ping: 测试服务是否运行
/// - /refresh: 触发资源数据库刷新
/// - /recompile: 触发脚本重新编译
/// </summary>
[InitializeOnLoad]
public static class McpRefreshServer
{
    static HttpListener _listener;       // HTTP 监听器
    static Thread _thread;                // 后台监听线程
    // 本地服务地址 - 可以用 http://localhost:5588/ 或 http://127.0.0.1:5588/
    const string Url = "http://127.0.0.1:5588/";

    /// <summary>
    /// 静态构造函数 - Unity Editor 启动时自动调用
    /// </summary>
    static McpRefreshServer()
    {
        TryStart();
        // 在 Unity 退出时停止服务
        EditorApplication.quitting += Stop;
    }

    /// <summary>
    /// 尝试启动 HTTP 服务器
    /// </summary>
    static void TryStart()
    {
        // 如果已经启动，直接返回
        if (_listener != null) return;
        
        try
        {
            // 创建并配置 HTTP 监听器
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
            _listener.Start();
        }
        catch (System.Exception e)
        {
            // 启动失败时给出详细提示（可能需要管理员权限配置）
            Debug.LogWarning($"[MCP Refresh] HttpListener 启动失败：{e.Message}\n" +
                             $"如为 Windows，请先在 PowerShell（管理员）执行：\n" +
                             $"netsh http add urlacl url={Url} user=Everyone");
            return;
        }

        // 创建后台监听线程
        _thread = new Thread(Listen) { IsBackground = true };
        _thread.Start();
        Debug.Log($"[MCP Refresh] 服务已启动：{Url} （/ping /refresh /recompile）");
    }

    /// <summary>
    /// 后台监听线程 - 处理 HTTP 请求
    /// </summary>
    static void Listen()
    {
        while (_listener != null && _listener.IsListening)
        {
            HttpListenerContext ctx = null;
            try 
            { 
                // 阻塞等待请求
                ctx = _listener.GetContext(); 
            }
            catch 
            { 
                // 监听器被停止或出错，退出循环
                break; 
            }
            
            if (ctx == null) continue;

            // 获取请求路径
            string path = ctx.Request.Url.AbsolutePath;
            
            // 简化响应的辅助函数
            void Ok(string s = "ok") => Write(ctx, s);
            
            // 根据不同路径执行不同操作
            switch (path)
            {
                case "/ping":
                    // 心跳检测 - 返回 "pong"
                    Ok("pong");
                    break;
                    
                case "/refresh":
                    // 刷新资源数据库
                    // 注意：必须在主线程执行 Unity Editor API
                    EditorApplication.delayCall += () =>
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    Ok();
                    break;
                    
                case "/recompile":
                    // 请求脚本重新编译
                    EditorApplication.delayCall +=
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation;
                    Ok();
                    break;
                    
                default:
                    // 未知路径
                    Write(ctx, "unknown");
                    break;
            }
        }
    }

    /// <summary>
    /// 发送 HTTP 响应
    /// </summary>
    static void Write(HttpListenerContext ctx, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentType = "text/plain; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    /// <summary>
    /// 停止 HTTP 服务器
    /// </summary>
    static void Stop()
    {
        try { _listener?.Stop(); _listener?.Close(); } catch { }
        try { _thread?.Interrupt(); _thread?.Join(100); } catch { }
        _listener = null; 
        _thread = null;
    }
}
#endif

