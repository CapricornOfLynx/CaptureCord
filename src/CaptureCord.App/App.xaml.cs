// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;

namespace CaptureCord.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--smoke-test"))
        {
            DispatcherUnhandledException += (_, error) =>
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "smoke-error.txt"), error.Exception.ToString());
                error.Handled = true;
                Environment.Exit(1);
            };
            MainWindow = new MainWindow(true) { ShowInTaskbar = false, Opacity = 0 };
            MainWindow.Show();
        }
        else
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        e.ApplicationExitCode = Environment.ExitCode;
        base.OnExit(e);
    }
}
