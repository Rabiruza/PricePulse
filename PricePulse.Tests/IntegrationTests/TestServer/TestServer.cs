using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PricePulse.Tests.IntegrationTests.TestServer;

public class TestServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;
    private readonly string _contentRoot;

    public string Url { get; }

    public TestServer(string prefix = "http://localhost:8080/")
    {
        Url = prefix;
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        // Find the TestServer directory where HTML files are located
        var assemblyPath = AppContext.BaseDirectory;
        var testServerDir = FindTestServerDirectory(assemblyPath);
        _contentRoot = testServerDir ?? Path.Combine(assemblyPath, "IntegrationTests", "TestServer");

        _listenTask = Task.Run(() => ListenAsync(_cts.Token));
    }

    private static string? FindTestServerDirectory(string startPath)
    {
        var current = startPath;
        while (!string.IsNullOrEmpty(current))
        {
            var testServerPath = Path.Combine(current, "IntegrationTests", "TestServer");
            if (Directory.Exists(testServerPath))
            {
                return testServerPath;
            }

            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                var requestUrl = context.Request.Url?.AbsolutePath ?? "/";

                await HandleRequestAsync(context, requestUrl, token);
            }
            catch (ObjectDisposedException)
            {
                // Server was stopped, exit the loop
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TestServer error: {ex}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, string requestUrl, CancellationToken token)
    {
        try
        {
            // Map URL path to HTML file
            var fileName = requestUrl.TrimStart('/');
            var filePath = Path.Combine(_contentRoot, fileName);

            // Security: Ensure we don't escape the content root
            var fullFilePath = Path.GetFullPath(filePath);
            var fullContentRoot = Path.GetFullPath(_contentRoot);
            
            if (!fullFilePath.StartsWith(fullContentRoot, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath, token);
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, token);
                context.Response.StatusCode = 200;
            }
            else
            {
                context.Response.StatusCode = 404;
                var notFoundMsg = "<html><body><h1>404 - Not Found</h1></body></html>";
                var buffer = System.Text.Encoding.UTF8.GetBytes(notFoundMsg);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex}");
            context.Response.StatusCode = 500;
        }
        finally
        {
            context.Response.Close();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        try
        {
            await _listenTask;
        }
        catch { }
        _cts.Dispose();
    }
}