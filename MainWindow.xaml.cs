using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Data.Sqlite;

namespace ShowNetLog
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<NetLogEntry> _logs = new ObservableCollection<NetLogEntry>();
        private ICollectionView? _logsView;
        private string _dbPath = @"C:\ProgramData\Locktime\NetLimiter\5\Stats\nlstats.db";

        public MainWindow()
        {
            InitializeComponent();
            _logsView = CollectionViewSource.GetDefaultView(_logs);
            if (_logsView != null)
            {
                _logsView.Filter = FilterLogs;
            }
            LogDataGrid.ItemsSource = _logsView;
            
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private bool FilterLogs(object obj)
        {
            if (obj is NetLogEntry entry)
            {
                string filterText = FilterTextBox.Text.ToLower();
                if (string.IsNullOrWhiteSpace(filterText)) return true;

                return (entry.AppPath?.ToLower().Contains(filterText) ?? false) ||
                       (entry.LocalIp?.ToLower().Contains(filterText) ?? false) ||
                       (entry.RemoteIp?.ToLower().Contains(filterText) ?? false) ||
                       (entry.LocalDomain?.ToLower().Contains(filterText) ?? false) ||
                       (entry.RemoteDomain?.ToLower().Contains(filterText) ?? false);
            }
            return false;
        }

        private void LoadData()
        {
            StatusTextBlock.Text = "Loading data...";
            _logs.Clear();

            if (!File.Exists(_dbPath))
            {
                MessageBox.Show($"Database file not found: {_dbPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Database not found.";
                return;
            }

            try
            {
                string connectionString = $"Data Source={_dbPath};Mode=ReadOnly";
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();

                    string sql = @"
SELECT 
    strftime('%Y-%m-%d %H:%M:%S', printf('%d', (c.TimeStart/1000 - 11644473600)), 'unixepoch', 'localtime') AS [StartTime],
    c.TimeDuration AS [Duration],
    a.Path AS [AppPath],
    c.Proto AS [Protocol],
    CASE 
        WHEN v4_local.Ip IS NOT NULL THEN 
            ((v4_local.Ip & 255) || '.' || ((v4_local.Ip >> 8) & 255) || '.' || ((v4_local.Ip >> 16) & 255) || '.' || ((v4_local.Ip >> 24) & 255))
        WHEN v6_local.Id IS NOT NULL THEN 
            printf('%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x',
                (v6_local.Ip1 & 255), ((v6_local.Ip1 >> 8) & 255), ((v6_local.Ip1 >> 16) & 255), ((v6_local.Ip1 >> 24) & 255),
                (v6_local.Ip2 & 255), ((v6_local.Ip2 >> 8) & 255), ((v6_local.Ip2 >> 16) & 255), ((v6_local.Ip2 >> 24) & 255),
                (v6_local.Ip3 & 255), ((v6_local.Ip3 >> 8) & 255), ((v6_local.Ip3 >> 16) & 255), ((v6_local.Ip3 >> 24) & 255),
                (v6_local.Ip4 & 255), ((v6_local.Ip4 >> 8) & 255), ((v6_local.Ip4 >> 16) & 255), ((v6_local.Ip4 >> 24) & 255)
            )
        ELSE 'Unknown'
    END AS [LocalIp],
    c.LocalPort AS [LocalPort],
    CASE 
        WHEN v4_remote.Ip IS NOT NULL THEN 
            ((v4_remote.Ip & 255) || '.' || ((v4_remote.Ip >> 8) & 255) || '.' || ((v4_remote.Ip >> 16) & 255) || '.' || ((v4_remote.Ip >> 24) & 255))
        WHEN v6_remote.Id IS NOT NULL THEN 
            printf('%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x:%02x%02x',
                (v6_remote.Ip1 & 255), ((v6_remote.Ip1 >> 8) & 255), ((v6_remote.Ip1 >> 16) & 255), ((v6_remote.Ip1 >> 24) & 255),
                (v6_remote.Ip2 & 255), ((v6_remote.Ip2 >> 8) & 255), ((v6_remote.Ip2 >> 16) & 255), ((v6_remote.Ip2 >> 24) & 255),
                (v6_remote.Ip3 & 255), ((v6_remote.Ip3 >> 8) & 255), ((v6_remote.Ip3 >> 16) & 255), ((v6_remote.Ip3 >> 24) & 255),
                (v6_remote.Ip4 & 255), ((v6_remote.Ip4 >> 8) & 255), ((v6_remote.Ip4 >> 16) & 255), ((v6_remote.Ip4 >> 24) & 255)
            )
        ELSE 'Unknown'
    END AS [RemoteIp],
    c.RemotePort AS [RemotePort],
    COALESCE(v4_local.DomName, v6_local.DomName, 'Unknown / No Domain') AS [LocalDomain],
    COALESCE(v4_remote.DomName, v6_remote.DomName, 'Unknown / No Domain') AS [RemoteDomain],
    c.TotalDataIn AS [DataIn],
    c.TotalDataOut AS [DataOut]
FROM Cnns c
LEFT JOIN Apps a ON c.AppId = a.Id
LEFT JOIN Users u ON c.UserId = u.Id
LEFT JOIN IpsV4 v4_local ON c.LocalIpId = v4_local.Ip
LEFT JOIN IpsV6 v6_local ON c.LocalIpId = v6_local.Id
LEFT JOIN IpsV4 v4_remote ON c.RemoteIpId = v4_remote.Ip
LEFT JOIN IpsV6 v6_remote ON c.RemoteIpId = v6_remote.Id
WHERE c.TotalDataIn > 0 OR c.TotalDataOut > 0
ORDER BY c.TimeStart DESC
LIMIT 1000;
";

                    var command = connection.CreateCommand();
                    command.CommandText = sql;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _logs.Add(new NetLogEntry
                            {
                                StartTime = reader.GetString(0),
                                Duration = reader.GetInt64(1),
                                AppPath = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Protocol = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                LocalIp = reader.GetString(4),
                                LocalPort = reader.GetInt32(5),
                                RemoteIp = reader.GetString(6),
                                RemotePort = reader.GetInt32(7),
                                LocalDomain = reader.GetString(8),
                                RemoteDomain = reader.GetString(9),
                                DataIn = reader.GetInt64(10),
                                DataOut = reader.GetInt64(11)
                            });
                        }
                    }
                }
                StatusTextBlock.Text = $"Loaded {_logs.Count} entries.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Error loading data.";
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _logsView?.Refresh();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}
