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

    public class PortFilterItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public int Port { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<NetLogEntry> _logs = new ObservableCollection<NetLogEntry>();
        private ObservableCollection<PortFilterItem> _portFilters = new ObservableCollection<PortFilterItem>();
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
            PortFilterComboBox.ItemsSource = _portFilters;
            
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private bool FilterLogs(object obj)
        {
            if (obj is NetLogEntry entry)
            {
                // Handle "Remote All" filter (Hide Unknown Remote IPs if unchecked)
                if (RemoteAllCheckBox.IsChecked == false && entry.RemoteIp == "Unknown")
                {
                    return false;
                }

                // Handle Port Filter
                var portFilter = _portFilters.FirstOrDefault(p => p.Port == entry.RemotePort);
                if (portFilter != null && !portFilter.IsSelected)
                {
                    return false;
                }

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

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            StatusTextBlock.Text = "Loading data...";
            LoadingProgressBar.Visibility = Visibility.Visible;
            LoadingProgressBar.IsIndeterminate = true;
            _logs.Clear();
            _portFilters.Clear();

            if (!File.Exists(_dbPath))
            {
                MessageBox.Show($"Database file not found: {_dbPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Database not found.";
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                string connectionString = $"Data Source={_dbPath};Mode=ReadOnly";
                using (var connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Step 1: Count rows for progress
                    var countCommand = connection.CreateCommand();
                    countCommand.CommandText = "SELECT COUNT(*) FROM Cnns WHERE TotalDataIn > 0 OR TotalDataOut > 0";
                    long totalRows = (long)(await countCommand.ExecuteScalarAsync() ?? 0L);
                    
                    if (totalRows > 1000) totalRows = 1000; // Matching the LIMIT in the main query

                    LoadingProgressBar.IsIndeterminate = false;
                    LoadingProgressBar.Maximum = totalRows;
                    LoadingProgressBar.Value = 0;

                    // Step 2: Load rows
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

                    var ports = new HashSet<int>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        int loadedCount = 0;
                        while (await reader.ReadAsync())
                        {
                            var entry = new NetLogEntry
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
                            };
                            _logs.Add(entry);
                            ports.Add(entry.RemotePort);
                            
                            loadedCount++;
                            if (loadedCount % 10 == 0) // Update progress every 10 rows
                            {
                                LoadingProgressBar.Value = loadedCount;
                                StatusTextBlock.Text = $"Loading {loadedCount} of {totalRows}...";
                                await System.Threading.Tasks.Task.Delay(1); // Yield to UI thread
                            }
                        }
                    }

                    foreach (var port in ports.OrderBy(p => p))
                    {
                        var filterItem = new PortFilterItem { Port = port };
                        filterItem.PropertyChanged += (s, ev) => _logsView?.Refresh();
                        _portFilters.Add(filterItem);
                    }
                }
                StatusTextBlock.Text = $"Loaded {_logs.Count} entries.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Error loading data.";
            }
            finally
            {
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _logsView?.Refresh();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }


        private void GroupByAppCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_logsView == null) return;

            _logsView.GroupDescriptions.Clear();
            if (GroupByAppCheckBox.IsChecked == true)
            {
                _logsView.GroupDescriptions.Add(new PropertyGroupDescription("AppPath"));
            }
        }

        private void RemoteAllCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _logsView?.Refresh();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var currentShowAll = LocalIpColumn.Visibility == Visibility.Visible;
            var settingsWin = new SettingsWindow(currentShowAll);
            settingsWin.Owner = this;
            settingsWin.AllColumnsChanged += (s, showAll) =>
            {
                var visibility = showAll ? Visibility.Visible : Visibility.Collapsed;
                LocalIpColumn.Visibility = visibility;
                LocalDomainColumn.Visibility = visibility;
            };
            settingsWin.ShowDialog();
        }
    }
}
