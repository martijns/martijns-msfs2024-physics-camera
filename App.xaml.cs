using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MsfsPhysicsCamera
{
    public partial class App : System.Windows.Application
    {
        private Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "MsfsPhysicsCameraInstanceMutex";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                ShowAlreadyRunningPopup();
            }
            else
            {
                base.OnStartup(e);
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }

        private void ShowAlreadyRunningPopup()
        {
            Window window = new Window
            {
                Title = "MSFS Physics Camera",
                Width = 350,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true
            };

            StackPanel panel = new StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock textBlock = new TextBlock
            {
                Text = "Another instance of MSFS Physics Camera is already running.",
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            System.Windows.Controls.Button exitButton = new System.Windows.Controls.Button
            {
                Content = "Exit (10)",
                Width = 100,
                Height = 30,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            int countdown = 10;
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            exitButton.Click += (s, ev) =>
            {
                timer.Stop();
                window.Close();
                Current.Shutdown();
            };

            timer.Tick += (s, ev) =>
            {
                countdown--;
                if (countdown <= 0)
                {
                    timer.Stop();
                    window.Close();
                    Current.Shutdown();
                }
                else
                {
                    exitButton.Content = $"Exit ({countdown})";
                }
            };

            panel.Children.Add(textBlock);
            panel.Children.Add(exitButton);
            window.Content = panel;

            window.Closed += (s, ev) => Current.Shutdown();

            timer.Start();
            window.Show();
        }
    }
}
