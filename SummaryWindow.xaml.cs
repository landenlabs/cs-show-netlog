// Copyright (c) 2026 LanDen Labs - Dennis Lang
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ShowNetLog
{
    public partial class SummaryWindow : Window
    {
        public class SummaryItem
        {
            public string? Key { get; set; }
            public int Count { get; set; }
            public long TotalDuration { get; set; }
        }

        public SummaryWindow(IEnumerable<NetLogEntry> data)
        {
            InitializeComponent();
            UpdateData(data);
        }

        public void UpdateData(IEnumerable<NetLogEntry> data)
        {
            var domainSummary = data
                .GroupBy(l => l.RemoteDomain ?? "Unknown")
                .Select(g => new SummaryItem 
                { 
                    Key = g.Key, 
                    Count = g.Count(), 
                    TotalDuration = g.Sum(l => l.Duration) 
                })
                .OrderByDescending(i => i.Count)
                .ToList();

            var portSummary = data
                .GroupBy(l => l.RemotePort.ToString())
                .Select(g => new SummaryItem 
                { 
                    Key = g.Key, 
                    Count = g.Count(), 
                    TotalDuration = g.Sum(l => l.Duration) 
                })
                .OrderByDescending(i => i.Count)
                .ToList();

            DomainsDataGrid.ItemsSource = domainSummary;
            PortsDataGrid.ItemsSource = portSummary;
        }
    }
}
