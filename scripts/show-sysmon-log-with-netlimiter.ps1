$ErrorActionPreference = 'SilentlyContinue'
$StartTime = (Get-Date).AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")

Write-Host "Fetching and resolving actual network connection logs... Please wait." -ForegroundColor Cyan

# Target Event ID 3 (Network Connection Initiated) instead of Event ID 22
$XPath = "*[System[(EventID=3) and TimeCreated[@SystemTime>='$StartTime']]]"
$Events = Get-WinEvent -LogName 'Microsoft-Windows-Sysmon/Operational' -FilterXPath $XPath

if ($Events) {
    $Report = foreach ($Event in $Events) {
        $Xml = [xml]$Event.ToXml()
        $Data = $Xml.Event.EventData.Data
        
        $DestIP   = ($Data | Where-Object {$_.Name -eq "DestinationIp"}).'#text'
        $DestPort = ($Data | Where-Object {$_.Name -eq "DestinationPort"}).'#text'
        $HostName = ($Data | Where-Object {$_.Name -eq "DestinationHostname"}).'#text'
        
        # Skip local/internal network traffic spikes
        if ($DestIP -match "^192\.168\." -or $DestIP -match "^10\." -or $DestIP -eq "127.0.0.1") { continue }
        
        # If Sysmon didn't capture a hostname natively, force a quick local lookup
        if ($null -eq $HostName -or $HostName -eq "") {
            $HostName = $DestIP
        }

        [PSCustomObject]@{
            Time          = $Event.TimeCreated
            AccessedTarget = $HostName
            DestinationIP  = $DestIP
            Port          = $DestPort
        }
    }

    if ($Report) {
        # Group and display everything cleanly
        $Report | Sort-Object AccessedTarget -Unique | Out-GridView -Title "Daily Network Traffic Connection Audit"
    } else {
        Write-Host "No outbound network connections recorded." -ForegroundColor Yellow
    }
} else {
    Write-Host "No network connection logs found in Sysmon." -ForegroundColor Yellow
}