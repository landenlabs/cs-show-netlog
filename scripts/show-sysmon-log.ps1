$ErrorActionPreference = 'SilentlyContinue'

# Get exact timestamp from 24 hours ago
$StartTime = (Get-Date).AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")

Write-Host "Fetching and parsing Sysmon logs... Stripping reverse lookup noise." -ForegroundColor Cyan

# Target Event ID 22 (DNS Queries tracked by Sysmon)
$XPath = "*[System[(EventID=22) and TimeCreated[@SystemTime>='$StartTime']]]"
$Events = Get-WinEvent -LogName 'Microsoft-Windows-Sysmon/Operational' -FilterXPath $XPath

if ($Events) {
    $Report = foreach ($Event in $Events) {
        $Xml = [xml]$Event.ToXml()
        $Data = $Xml.Event.EventData.Data
        
        $Domain = ($Data | Where-Object {$_.Name -eq "QueryName"}).'#text'
        $IPs    = ($Data | Where-Object {$_.Name -eq "QueryResults"}).'#text'
        $Client = Split-Path (($Data | Where-Object {$_.Name -eq "Image"}).'#text') -Leaf

        # --- THE NOISE FILTER ---
        # 1. Skip null/empty entries
        if ($null -eq $Domain -or $Domain -eq "") { continue }
        
        # 2. Skip local loopbacks
        if ($Domain -eq "localhost" -or $Domain -match "127\.0\.0\.1") { continue }
        
        # 3. Skip Reverse IPv4 and IPv6 lookups (*.arpa)
        if ($Domain -like "*.arpa" -or $Domain -like "*.arpa.") { continue }
        
        # 4. Skip local Windows NetBIOS/mDNS machine discovery names (single words or local links)
        if ($Domain -notmatch "\.") { continue }
        if ($Domain -like "*.local" -or $Domain -like "*.lan") { continue }

        [PSCustomObject]@{
            Time          = $Event.TimeCreated
            DomainQueried = $Domain
            InitiatedBy   = $Client
            ResolvedIPs   = $IPs
        }
    }

    if ($Report) {
        # Sort alphabetically by clean domain name
        $Report | Sort-Object DomainQueried -Unique | Out-GridView -Title "Clean Outbound Web Traffic Audit"
    } else {
        Write-Host "All captured traffic consisted of background system lookups (.arpa / local)." -ForegroundColor Yellow
    }
} else {
    Write-Host "No outbound internet DNS requests captured by Sysmon." -ForegroundColor Yellow
}