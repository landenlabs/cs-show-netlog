# Suppress background errors if logs are empty or clearing out
$ErrorActionPreference = 'SilentlyContinue'

# Get exact timestamp from 24 hours ago formatted for the Windows Event API
$StartTime = (Get-Date).AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")

Write-Host "Fetching and parsing Windows DNS Client logs... Please wait." -ForegroundColor Cyan

# Fast XPath Query: Targets Event ID 3006 (DNS Query Initiated) within the 24-hour window
$XPath = "*[System[(EventID=3006) and TimeCreated[@SystemTime>='$StartTime']]]"

# Fetch the raw events natively
$Events = Get-WinEvent -LogName 'Microsoft-Windows-DNS-Client/Operational' -FilterXPath $XPath

if ($Events) {
    $Report = foreach ($Event in $Events) {
        # Convert the Event XML payload into a navigable object
        $Xml = [xml]$Event.ToXml()
        $Data = $Xml.Event.EventData.Data
        
        # Extract the human-readable domain name string
        $Domain = ($Data | Where-Object {$_.Name -eq "QueryName"}).'#text'
        
        # --- HOSTS FILE FILTER ---
        # If the domain matches common local host triggers, skip it to prevent log bloat
        if ($null -eq $Domain -or 
            $Domain -eq "localhost" -or 
            $Domain -match "127\.0\.0\.1" -or
            $Domain -match "::1") {
            continue
        }
        
        # Add any of your specific 3 local hosts file domains here to explicitly strip them out:
        # Example: if ($Domain -match "my-local-test-site.com") { continue }

        [PSCustomObject]@{
            Time          = $Event.TimeCreated
            DomainQueried = $Domain
        }
    }

    # Remove exact duplicates and output to an interactive, searchable GUI Grid
    if ($Report) {
        $Report | Sort-Object DomainQueried -Unique | Out-GridView -Title "Daily Windows DNS Domains Report"
    } else {
        Write-Host "All logged DNS traffic was filtered out as local hosts noise." -ForegroundColor Yellow
    }
} else {
    Write-Host "No DNS traffic found in the Windows log for the last 24 hours." -ForegroundColor Yellow
}