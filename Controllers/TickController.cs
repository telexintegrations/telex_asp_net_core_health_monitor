using API.Contracts;
using API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

[ApiController]
[Route("/tick")]
public class TickController : ControllerBase
{
    private readonly IWebhookService _service;
    private readonly HealthCheckService _healthCheckService;
    private readonly IConfiguration _configuraitons;

    public TickController(IWebhookService service, HealthCheckService healthCheckService, IConfiguration configurations)
    {
        _service = service;
        _healthCheckService = healthCheckService;
        _configuraitons = configurations;
    }

    [HttpPost]
    public async Task<IActionResult> PostTick([FromBody] Payload payload)
    {
        if (payload == null)
        {
            return BadRequest("Invalid payload.");
        }

        if (string.IsNullOrEmpty(payload.ReturnUrl) || string.IsNullOrEmpty(payload.ChannelId))
        {
            return BadRequest("Return URL and Channel Id are required.");
        }

        var healthReport = await _healthCheckService.CheckHealthAsync();

        var systemHealthCheckEntry = healthReport.Entries["system_health_check"];
        var cpuUsage = systemHealthCheckEntry.Data["CPU"];
        var ramUsageMB = systemHealthCheckEntry.Data["RAM"];
        var diskUsage = systemHealthCheckEntry.Data["Disk"] as Dictionary<string, object>;
        var networkActivity = systemHealthCheckEntry.Data["Network"] as Dictionary<string, object>;
        var gcMetrics = systemHealthCheckEntry.Data["GC"] as Dictionary<string, object>;
        var threadCount = systemHealthCheckEntry.Data["Threads"];
        var uptime = systemHealthCheckEntry.Data["Uptime"];
        
        var message = $@"
        Health status: {healthReport.Status}
        CPU Usage: {cpuUsage}
        RAM Usage: {ramUsageMB}
        Disk Usage:
        Total Size: {diskUsage?["TotalSize"]}
        - Available Free Space: {diskUsage?["AvailableFreeSpace"]}
        - Used Space: {diskUsage?["UsedSpace"]}
        Network Activity:
        - Bytes Sent: {networkActivity?["BytesSent"]}
        - Bytes Received: {networkActivity?["BytesReceived"]}
        GC Metrics:
        - Total Memory: {gcMetrics?["TotalMemory"]}
        - Total Collections (Gen 0): {gcMetrics?["TotalCollections"]}
        Thread Count: {threadCount}
        Uptime: {uptime}
        ";
        var webhookPayload = new
        {
            event_name = "Health Check",
            message = message,
            status = "success",
            username = "system"
        };

        try
        {
            await _service.SendWebhookAsync(payload.ReturnUrl, webhookPayload);
            return Ok("Webhook sent successfully.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to send webhook: {ex.Message}");
        }
    }
}
