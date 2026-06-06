using System;
using System.Reflection;
using System.Windows;

namespace ShowNetLog
{
    public partial class SettingsWindow : Window
    {
        public bool ShowAllColumns { get; private set; }
        public event EventHandler<bool>? AllColumnsChanged;

        public SettingsWindow(bool currentShowAll)
        {
            InitializeComponent();
            ShowAllColumns = currentShowAll;
            AllColumnsCheckBox.IsChecked = ShowAllColumns;

            VersionText.Text = $"Version: {Assembly.GetExecutingAssembly().GetName().Version}";
            
            // Getting build date from file info as a proxy for compile date
            var fileInfo = new System.IO.FileInfo(Assembly.GetExecutingAssembly().Location);
            BuildDateText.Text = $"Build Date: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
        }

        private void AllColumnsCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ShowAllColumns = AllColumnsCheckBox.IsChecked ?? false;
            AllColumnsChanged?.Invoke(this, ShowAllColumns);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
