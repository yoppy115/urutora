param(
    [string]$TailnetIp = "100.100.176.17",
    [int]$Port = 8765
)

$ErrorActionPreference = "Stop"
$ruleName = "ARK Server Manager (Tailscale only)"

Write-Host ""
Write-Host "ARK Server Manager - iPhone connection setup" -ForegroundColor Cyan
Write-Host "Only Tailscale addresses are allowed through Windows Firewall." -ForegroundColor DarkGray
Write-Host ""

try {
    $address = [Net.IPAddress]::Parse($TailnetIp)
    $bytes = $address.GetAddressBytes()
    if ($bytes.Length -ne 4 -or $bytes[0] -ne 100 -or $bytes[1] -lt 64 -or $bytes[1] -gt 127) {
        throw "The address is not in Tailscale's 100.64.0.0/10 range: $TailnetIp"
    }
    if ($Port -lt 1024 -or $Port -gt 65535) {
        throw "Invalid port: $Port"
    }

    $rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if ($null -eq $rule) {
        $rule = New-NetFirewallRule `
            -DisplayName $ruleName `
            -Description "Allows ARK Server Manager mobile UI only from Tailscale devices." `
            -Direction Inbound `
            -Action Allow `
            -Protocol TCP `
            -LocalAddress $TailnetIp `
            -LocalPort $Port `
            -RemoteAddress "100.64.0.0/10" `
            -Profile Any
    } else {
        $rule | Set-NetFirewallRule -Enabled True -Direction Inbound -Action Allow -Profile Any
        $rule | Get-NetFirewallPortFilter | Set-NetFirewallPortFilter -Protocol TCP -LocalPort $Port
        $rule | Get-NetFirewallAddressFilter | Set-NetFirewallAddressFilter -LocalAddress $TailnetIp -RemoteAddress "100.64.0.0/10"
    }

    $url = "http://${TailnetIp}:$Port"
    $cacheDirectory = Join-Path $env:APPDATA "ARK Server Manager"
    New-Item -ItemType Directory -Path $cacheDirectory -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $cacheDirectory "remote-url.txt") -Value $url -Encoding UTF8
    try { Set-Clipboard -Value $url } catch { }

    Write-Host "Windows Firewall setup is complete." -ForegroundColor Green
    Write-Host "Open this URL in Safari while Tailscale is connected:" -ForegroundColor Green
    Write-Host $url -ForegroundColor Cyan
    Write-Host "The URL has also been copied to the PC clipboard." -ForegroundColor DarkGray
} catch {
    Write-Host "Setup failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}

Write-Host ""
Read-Host "Press Enter to close"
