using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using Wochenplanung.Domain.Entities;
using Wochenplanung.Domain.ValueObjects;

namespace Wochenplanung.Presentation.Services;

public sealed class PlanShareHost : IDisposable
{
    public const int DefaultPort = 5088;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Func<string>? _html;

    public bool IsRunning => _listener?.IsListening == true;
    public string? PublicUrl { get; private set; }

    public void Start(Func<string> htmlFactory, int port = DefaultPort)
    {
        Stop();
        _html = htmlFactory;
        var ip = GetLanIPv4() ?? "127.0.0.1";
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add($"http://localhost:{port}/");
        try
        {
            _listener.Prefixes.Add($"http://{ip}:{port}/");
        }
        catch
        {
            // LAN prefix optional
        }

        _listener.Start();
        PublicUrl = $"http://{ip}:{port}/";
        _cts = new CancellationTokenSource();
        _ = ListenAsync(_cts.Token);
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (_listener != null)
            {
                _listener.Close();
                _listener = null;
            }
        }
        catch
        {
            // ignore
        }

        PublicUrl = null;
        _html = null;
    }

    public void Dispose() => Stop();

    public static string BuildHtml(WeekInfo week, IReadOnlyList<Employee> employees, IReadOnlyList<WorkTask> tasks)
    {
        var days = week.GetDays(includeWeekend: false);
        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html><html lang="de"><head><meta charset="utf-8"/>
            <meta name="viewport" content="width=device-width, initial-scale=1"/>
            <title>Meisterfenster Plan</title>
            <style>
            body{font-family:Segoe UI,sans-serif;margin:12px;background:#f4f4f4;color:#222}
            h1{font-size:1.2rem}
            table{border-collapse:collapse;width:100%;background:#fff}
            th,td{border:1px solid #ccc;padding:6px;vertical-align:top;font-size:13px}
            th{background:#e8eef5}
            .task{margin:0 0 6px;padding:4px 6px;border-radius:6px;background:#ffe0c2}
            .muted{color:#666;font-size:12px}
            </style></head><body>
            """);
        sb.Append($"<h1>Wochenplan KW {week.WeekNumber} / {week.Year}</h1>");
        sb.Append($"<p class=\"muted\">{week.StartDate:dd.MM.yyyy} – {week.StartDate.AddDays(4):dd.MM.yyyy} · nur lesen · {DateTime.Now:HH:mm}</p>");
        sb.Append("<table><thead><tr><th>Mitarbeiter</th>");
        foreach (var day in days)
            sb.Append($"<th>{day:ddd dd.MM.}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var employee in employees.Where(e => e.IsActive))
        {
            sb.Append($"<tr><td><strong>{WebUtility.HtmlEncode(employee.DisplayName)}</strong></td>");
            foreach (var day in days)
            {
                sb.Append("<td>");
                foreach (var task in tasks.Where(t => t.EmployeeId == employee.Id && Covers(t, day)))
                {
                    var time = task.StartTime.HasValue
                        ? task.StartTime.Value.ToString(@"hh\:mm") + (task.EndTime.HasValue ? "–" + task.EndTime.Value.ToString(@"hh\:mm") : "")
                        : "";
                    sb.Append("<div class=\"task\">");
                    sb.Append(WebUtility.HtmlEncode(task.Description));
                    if (!string.IsNullOrWhiteSpace(time))
                        sb.Append($"<div class=\"muted\">{time}</div>");
                    sb.Append("</div>");
                }
                sb.Append("</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></body></html>");
        return sb.ToString();
    }

    private static bool Covers(WorkTask task, DateTime day)
    {
        var start = task.StartDate.Date;
        var end = (task.EndDate ?? task.StartDate).Date;
        return start <= day.Date && end >= day.Date;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (Exception)
            {
                break;
            }

            _ = Task.Run(() => WriteResponse(ctx), cancellationToken);
        }
    }

    private void WriteResponse(HttpListenerContext ctx)
    {
        try
        {
            var html = System.Windows.Application.Current?.Dispatcher.Invoke(() => _html?.Invoke() ?? "<p>Kein Plan.</p>")
                       ?? "<p>Kein Plan.</p>";
            var buffer = Encoding.UTF8.GetBytes(html);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Close();
        }
        catch
        {
            try { ctx.Response.Abort(); } catch { }
        }
    }

    private static string? GetLanIPv4()
    {
        try
        {
            foreach (var addr in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                    return addr.ToString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
