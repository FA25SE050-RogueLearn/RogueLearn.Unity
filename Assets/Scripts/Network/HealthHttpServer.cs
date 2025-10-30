// Minimal health endpoint for orchestration. Safe to include in all builds; it only runs in batch mode.
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BossFight2D.Network
{
    public class HealthHttpServer : MonoBehaviour
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            // Only run in headless/batch mode to avoid affecting client builds.
            if (!Application.isBatchMode)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            int port = 0;
            var env = Environment.GetEnvironmentVariable("HEALTH_PORT");
            if (!int.TryParse(env, out port) || port <= 0) port = 8080;
            var prefix = $"http://*:{port}/";

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                Debug.Log($"[HealthHttpServer] Listening on {prefix}");
                _ = Task.Run(() => ServeAsync(_cts.Token));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HealthHttpServer] Failed to start: {e.Message}");
            }
        }

        private async Task ServeAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    var req = ctx.Request;
                    var res = ctx.Response;

                    if (req.Url != null && req.Url.AbsolutePath.Equals("/healthz", StringComparison.OrdinalIgnoreCase))
                    {
                        var payload = Encoding.UTF8.GetBytes("ok");
                        res.StatusCode = 200;
                        res.ContentType = "text/plain";
                        await res.OutputStream.WriteAsync(payload, 0, payload.Length, ct);
                    }
                    else
                    {
                        res.StatusCode = 404;
                    }

                    res.Close();
                }
                catch (ObjectDisposedException)
                {
                    // Listener was closed (likely during scene switch or shutdown). Exit quietly.
                    if (!ct.IsCancellationRequested)
                    {
                        Debug.LogWarning("[HealthHttpServer] Listener disposed; stopping.");
                    }
                    break;
                }
                catch (HttpListenerException ex)
                {
                    // If stopping, exit quietly; otherwise log and continue
                    if (ct.IsCancellationRequested || _listener == null || !_listener.IsListening)
                    {
                        break;
                    }
                    Debug.LogWarning($"[HealthHttpServer] Listener exception: {ex.Message}");
                }
                catch (Exception e)
                {
                    // Swallow transient errors; shut down gracefully via OnDestroy
                    Debug.LogError($"[HealthHttpServer] Listener error: {e.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Close();
            }
            catch { /* ignore */ }
        }
    }
}