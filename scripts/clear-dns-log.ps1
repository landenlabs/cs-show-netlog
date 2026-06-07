
# older way of clearing
# Clear-EventLog -LogName "Microsoft-Windows-DNS-Client/Operational" -ErrorAction SilentlyContinue

# new way of clearing DNS log
wevtutil cl Microsoft-Windows-DNS-Client/Operational
ipconfig /flushdns