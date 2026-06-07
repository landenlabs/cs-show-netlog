// Copyright (c) 2026 LanDen Labs - Dennis Lang
using System.Configuration;
using System.Data;
using System.Windows;

namespace ShowNetLog
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Handle exceptions on non-UI threads
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => 
                LogException(ev.ExceptionObject as Exception, "AppDomain.UnhandledException");

            // Handle exceptions in async Tasks
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ev) => 
            {
                LogException(ev.Exception, "TaskScheduler.UnobservedTaskException");
                ev.SetObserved();
            };

            base.OnStartup(e);
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception, "DispatcherUnhandledException");
            // We don't set e.Handled = true because we want the app to crash/exit as it normally would,
            // but only after we've logged the details.
        }

        private void LogException(Exception? ex, string source)
        {
            if (ex == null) return;

            string message = $"\n[CRASH] {source} caught an unhandled exception:\n" +
                             $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                             $"Message: {ex.Message}\n" +
                             $"Type: {ex.GetType().FullName}\n" +
                             $"StackTrace:\n{ex.StackTrace}\n";

            if (ex.InnerException != null)
            {
                message += $"Inner Exception: {ex.InnerException.Message}\n" +
                           $"Inner StackTrace:\n{ex.InnerException.StackTrace}\n";
            }

            Console.Error.WriteLine(message);
            
            // In a WinExe, Console.Error might not be visible unless launched from a terminal.
            // We can also use Trace or Debug which will show in the Output window of VS.
            System.Diagnostics.Trace.WriteLine(message);
        }
    }
}
