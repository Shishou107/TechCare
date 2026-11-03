using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using API_FixComputerCenter.Application.Common.Exceptions;
using API_FixComputerCenter.Application.Common.Interfaces;
using API_FixComputerCenter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace API_FixComputerCenter.Application.Features.Diagnostics;

public class DiskSerialInfo
{
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? SizeGB { get; set; }
}

public class RamModuleInfo
{
    public string? SerialNumber { get; set; }
    public string? PartNumber { get; set; }
    public decimal? CapacityGB { get; set; }
}

public class DiagnosticScanPayload
{
    public string? SessionCode { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? Cpu { get; set; }
    public string? Ram { get; set; }
    public string? Storage { get; set; }
    public string? OperatingSystem { get; set; }
    public List<DiskSerialInfo>? DiskSerials { get; set; }
    public List<RamModuleInfo>? RamModules { get; set; }
    public string? ScannedAt { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
}

public record SaveDiagnosticRunRequest(
    long RepairTicketId,
    string? AgentVersion,
    DiagnosticScanPayload DeviceSpecs
);

public interface IDiagnosticService
{
    void SaveQuickSyncPayload(string sessionCode, DiagnosticScanPayload payload);
    DiagnosticScanPayload? GetQuickSyncPayload(string sessionCode);
    byte[] GenerateScanScriptBytes(string sessionCode, string? apiBaseUrl = null);
    Task<long> SaveDiagnosticRunAsync(SaveDiagnosticRunRequest req, CancellationToken ct = default);
}

public class DiagnosticService : IDiagnosticService
{
    private static readonly ConcurrentDictionary<string, (DiagnosticScanPayload Payload, DateTime ExpiryUtc)> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex SafeCodeRegex = new("^[a-zA-Z0-9_-]{4,64}$", RegexOptions.Compiled);
    private readonly ITechCareDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public DiagnosticService(ITechCareDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    public void SaveQuickSyncPayload(string sessionCode, DiagnosticScanPayload payload)
    {
        ValidateSessionCode(sessionCode);
        CleanupExpiredSessions();

        payload.ReceivedAtUtc = DateTime.UtcNow;
        _sessions[sessionCode] = (payload, DateTime.UtcNow.AddHours(1));
    }

    public DiagnosticScanPayload? GetQuickSyncPayload(string sessionCode)
    {
        ValidateSessionCode(sessionCode);
        CleanupExpiredSessions();

        if (_sessions.TryGetValue(sessionCode, out var item))
        {
            if (item.ExpiryUtc > DateTime.UtcNow)
            {
                return item.Payload;
            }
            _sessions.TryRemove(sessionCode, out _);
        }
        return null;
    }

    public byte[] GenerateScanScriptBytes(string sessionCode, string? apiBaseUrl = null)
    {
        ValidateSessionCode(sessionCode);

        var baseUrl = !string.IsNullOrWhiteSpace(apiBaseUrl)
            ? apiBaseUrl.TrimEnd('/')
            : "http://localhost:5000";

        var targetUrl = $"{baseUrl}/api/v1/diagnostics/quick-sync/{sessionCode}";

        var lines = new[]
        {
            "@echo off",
            "chcp 65001 >nul",
            "title TechCare - Diagnostic Auto Scan",
            "echo ==========================================================",
            "echo        TECHCARE LAPTOP SERVICE - AUTO HARDWARE SCAN       ",
            "echo ==========================================================",
            $"echo Dang doc cau hinh va tu dong dong bo vao don (Ma: {sessionCode})...",
            "powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"try { " +
            "$clean = { param($v) if (-not $v) { return $null }; $t = $v.ToString().Trim(); if ($t -eq '' -or $t -match '^[0\\s]+$' -or $t.ToLower() -in @('0','none','unknown','to be filled by o.e.m.','default string','not applicable','n/a','system serial number')) { return $null }; return $t }; " +
            "$cs = Get-CimInstance Win32_ComputerSystem; " +
            "$bios = Get-CimInstance Win32_Bios; " +
            "$machineSn = & $clean $bios.SerialNumber; " +
            "$cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name -replace '\\s+',' '; " +
            "$mem = Get-CimInstance Win32_PhysicalMemory; " +
            "$ramGB = [Math]::Round(($mem | Measure Capacity -Sum).Sum/1GB); " +
            "$diskItems = @(Get-CimInstance Win32_DiskDrive | ForEach-Object { $sn = & $clean $_.SerialNumber; $sz = if ($_.Size) { [Math]::Round($_.Size/1GB) } else { 0 }; $md = if ($_.Model) { $_.Model.Trim() } else { 'Disk Drive' }; [PSCustomObject]@{ Model = $md; SerialNumber = $sn; SizeGB = $sz } }); " +
            "$ramItems = @(Get-CimInstance Win32_PhysicalMemory | ForEach-Object { $sn = & $clean $_.SerialNumber; $pn = & $clean $_.PartNumber; $cap = if ($_.Capacity) { [Math]::Round($_.Capacity/1GB) } else { 0 }; [PSCustomObject]@{ SerialNumber = $sn; PartNumber = $pn; CapacityGB = $cap } }); " +
            "$disks = ($diskItems | ForEach-Object { '' + $_.SizeGB + 'GB ' + $_.Model }) -join ' | '; " +
            "$os = (Get-CimInstance Win32_OperatingSystem).Caption; " +
            $"$payload = [PSCustomObject]@{{ SessionCode = '{sessionCode}'; Manufacturer = if ($cs.Manufacturer) {{ $cs.Manufacturer.Trim() }} else {{ '' }}; Model = if ($cs.Model) {{ $cs.Model.Trim() }} else {{ '' }}; SerialNumber = $machineSn; Cpu = $cpu; Ram = ($ramGB.ToString() + ' GB'); Storage = $disks; OperatingSystem = $os; DiskSerials = $diskItems; RamModules = $ramItems }}; " +
            "$json = $payload | ConvertTo-Json -Depth 4; " +
            $"Invoke-RestMethod -Uri '{targetUrl}' -Method Post -Body $json -ContentType 'application/json; charset=utf-8' -TimeoutSec 5; " +
            "Write-Host '[OK] Da tu dong nhung cau hinh va serial linh kien vao he thong!' -ForegroundColor Green; " +
            "} catch { Write-Host '[!] Khong the ket noi API. Vui long kiem tra mang hoac nhap tay.' -ForegroundColor Yellow; } " +
            "Start-Sleep -Seconds 3;\"",
            "exit"
        };

        var content = string.Join("\r\n", lines);
        return System.Text.Encoding.UTF8.GetBytes(content);
    }

    public async Task<long> SaveDiagnosticRunAsync(SaveDiagnosticRunRequest req, CancellationToken ct = default)
    {
        var ticket = await _db.RepairTickets.FirstOrDefaultAsync(t => t.RepairTicketId == req.RepairTicketId, ct)
            ?? throw new NotFoundException(nameof(RepairTicket), req.RepairTicketId);

        var json = JsonSerializer.Serialize(req.DeviceSpecs);
        var run = new DiagnosticRun
        {
            RepairTicketId = ticket.RepairTicketId,
            CollectedByUserId = _currentUser.UserId,
            AgentVersion = req.AgentVersion ?? "1.0.0",
            ConsentRecordedAtUtc = DateTime.UtcNow,
            CollectedAtUtc = DateTime.UtcNow,
            DeviceSummaryJson = json
        };

        _db.DiagnosticRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("SAVE_DIAGNOSTIC_RUN", nameof(DiagnosticRun), run.DiagnosticRunId.ToString(), null, run, ct);
        return run.DiagnosticRunId;
    }

    private static void ValidateSessionCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !SafeCodeRegex.IsMatch(code))
        {
            throw new ValidationException("Mã phiên quét cấu hình không đúng định dạng an toàn (chỉ chấp nhận ký tự chữ, số, gạch nối từ 4-64 ký tự).");
        }
    }

    private static void CleanupExpiredSessions()
    {
        if (_sessions.Count > 100)
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _sessions)
            {
                if (kvp.Value.ExpiryUtc <= now)
                {
                    _sessions.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
