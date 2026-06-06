using System;

namespace ShowNetLog
{
    public class NetLogEntry
    {
        public string? StartTime { get; set; }
        public long Duration { get; set; }
        public string? AppPath { get; set; }
        public string? Protocol { get; set; }
        public string? LocalIp { get; set; }
        public int LocalPort { get; set; }
        public string? RemoteIp { get; set; }
        public int RemotePort { get; set; }
        public string? LocalDomain { get; set; }
        public string? RemoteDomain { get; set; }
        public long DataIn { get; set; }
        public long DataOut { get; set; }
    }
}
