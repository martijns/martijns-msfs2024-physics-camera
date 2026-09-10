using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace MsfsPhysicsCamera
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private SimConnectHandler? simConnect;
        private FreeTrackInjector? injector;
        private PhysicsEngine physics;
        private Stopwatch stopwatch;
        private double lastTime;
        private bool isRunning = true;
        private Thread physicsThread;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Binding Properties
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        
        public double VelX { get; set; }
        public double VelY { get; set; }
        public double VelZ { get; set; }
        
        public double PlanePitch { get; set; }
        public double PlaneBank { get; set; }

        public string StatusText { get; set; } = "Initializing...";

        public double HeadX { get; set; }
        public double HeadY { get; set; }
        public double HeadZ { get; set; }
        public double HeadPitch { get; set; }
        public double HeadRoll { get; set; }

        private double _effectMultiplier = 1.0;
        public double EffectMultiplier
        {
            get => _effectMultiplier;
            set
            {
                if (_effectMultiplier != value)
                {
                    _effectMultiplier = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectMultiplier)));
                    SaveSettings();
                }
            }
        }

        private double _transXMultiplier = 1.0;
        public double TransXMultiplier
        {
            get => _transXMultiplier;
            set { if (_transXMultiplier != value) { _transXMultiplier = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransXMultiplier))); SaveSettings(); } }
        }

        private double _transYMultiplier = 1.0;
        public double TransYMultiplier
        {
            get => _transYMultiplier;
            set { if (_transYMultiplier != value) { _transYMultiplier = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransYMultiplier))); SaveSettings(); } }
        }

        private double _transZMultiplier = 1.0;
        public double TransZMultiplier
        {
            get => _transZMultiplier;
            set { if (_transZMultiplier != value) { _transZMultiplier = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TransZMultiplier))); SaveSettings(); } }
        }

        private double _pitchMultiplier = 1.0;
        public double PitchMultiplier
        {
            get => _pitchMultiplier;
            set { if (_pitchMultiplier != value) { _pitchMultiplier = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PitchMultiplier))); SaveSettings(); } }
        }

        private double _rollMultiplier = 1.0;
        public double RollMultiplier
        {
            get => _rollMultiplier;
            set { if (_rollMultiplier != value) { _rollMultiplier = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RollMultiplier))); SaveSettings(); } }
        }

        private AppSettings settings;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            CheckRegistryStatus();

            settings = AppSettings.Load();
            _effectMultiplier = settings.EffectMultiplier;
            _transXMultiplier = settings.TransXMultiplier;
            _transYMultiplier = settings.TransYMultiplier;
            _transZMultiplier = settings.TransZMultiplier;
            _pitchMultiplier = settings.PitchMultiplier;
            _rollMultiplier = settings.RollMultiplier;

            if (settings.WindowTop.HasValue && settings.WindowLeft.HasValue)
            {
                double left = settings.WindowLeft.Value;
                double top = settings.WindowTop.Value;

                // Ensure the window is placed within the current virtual screen bounds
                if (left >= SystemParameters.VirtualScreenLeft &&
                    top >= SystemParameters.VirtualScreenTop &&
                    left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 50 &&
                    top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50)
                {
                    this.Left = left;
                    this.Top = top;
                }
            }

            physics = new PhysicsEngine();
            stopwatch = new Stopwatch();

            // Start logic in a background thread
            physicsThread = new Thread(PhysicsLoop) { IsBackground = true };
            physicsThread.Start();

            // Update UI at 60fps
            CompositionTarget.Rendering += UpdateUI;
        }

        private void SaveSettings()
        {
            if (settings != null)
            {
                settings.EffectMultiplier = _effectMultiplier;
                settings.TransXMultiplier = _transXMultiplier;
                settings.TransYMultiplier = _transYMultiplier;
                settings.TransZMultiplier = _transZMultiplier;
                settings.PitchMultiplier = _pitchMultiplier;
                settings.RollMultiplier = _rollMultiplier;
                settings.Save();
            }
        }

        private void PhysicsLoop()
        {
            try
            {
                simConnect = new SimConnectHandler();
                injector = new FreeTrackInjector();
                simConnect.Connect();
                
                double timeSinceLastReconnect = 0.0;
                
                stopwatch.Start();
                lastTime = stopwatch.Elapsed.TotalSeconds;

                while (isRunning)
                {
                    double currentTime = stopwatch.Elapsed.TotalSeconds;
                    double dt = currentTime - lastTime;
                    lastTime = currentTime;

                    if (!simConnect.IsConnected)
                    {
                        timeSinceLastReconnect += dt;
                        if (timeSinceLastReconnect > 2.0)
                        {
                            timeSinceLastReconnect = 0.0;
                            Dispatcher.Invoke(() => StatusText = "Attempting to connect to MSFS...");
                            simConnect.Connect();
                        }
                    }
                    else
                    {
                        var data = simConnect.CurrentData;
                        physics.Update(data, dt, EffectMultiplier, TransXMultiplier, TransYMultiplier, TransZMultiplier, PitchMultiplier, RollMultiplier);

                        // FreeTrack expects translations in millimeters and rotations in radians. 
                        // The base output was misjudged by roughly a factor of 16.
                        float ftX = (float)(physics.HeadX * 160.0);
                        float ftY = (float)(physics.HeadY * 160.0);
                        float ftZ = (float)(physics.HeadZ * 160.0);
                        float ftPitch = (float)(physics.HeadPitch * 16.0);
                        float ftRoll = (float)(physics.HeadRoll * 16.0);
                        float ftYaw = 0f;

                        injector.Update(ftX, ftY, ftZ, ftPitch, ftRoll, ftYaw);
                    }

                    Thread.Sleep(16); // ~60Hz
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => StatusText = "Error: " + ex.Message);
            }
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            if (simConnect != null && simConnect.IsConnected)
            {
                StatusText = "Connected to MSFS SimConnect";
                
                var data = simConnect.CurrentData;
                AccelX = data.AccelX;
                AccelY = data.AccelY;
                AccelZ = data.AccelZ;
                VelX = data.VelX;
                VelY = data.VelY;
                VelZ = data.VelZ;
                PlanePitch = data.Pitch * (180.0 / Math.PI); // Convert to degrees for easier reading
                PlaneBank = data.Bank * (180.0 / Math.PI); // Convert to degrees

                HeadX = physics.HeadX;
                HeadY = physics.HeadY;
                HeadZ = physics.HeadZ;
                HeadPitch = physics.HeadPitch;
                HeadRoll = physics.HeadRoll;

                // Notify UI
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            }
            else
            {
                StatusText = "Waiting for MSFS connection...";
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                settings.WindowTop = this.Top;
                settings.WindowLeft = this.Left;
            }
            else
            {
                settings.WindowTop = this.RestoreBounds.Top;
                settings.WindowLeft = this.RestoreBounds.Left;
            }
            settings.Save();

            isRunning = false;
            physicsThread?.Join(1000);
            simConnect?.Dispose();
            injector?.Dispose();
        }

        private void CheckRegistryStatus()
        {
            Dispatcher.Invoke(() =>
            {
                bool isCorrectlyConfigured = false;
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\NaturalPoint\NATURALPOINT\NPClient Location");
                    if (key != null)
                    {
                        var pathValue = key.GetValue("Path") as string;
                        if (!string.IsNullOrEmpty(pathValue))
                        {
                            string dllPath = Path.Combine(pathValue, "NPClient64.dll");
                            if (File.Exists(dllPath))
                            {
                                isCorrectlyConfigured = true;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors
                }

                if (isCorrectlyConfigured)
                {
                    TrackingStatusIcon.Fill = new SolidColorBrush(Colors.Green);
                    TrackingStatusText.Text = "Head tracking seems to be configured correctly in Flight Simulator 2024. You should be good to go.";
                    TrackingRestartMsg.Visibility = Visibility.Collapsed;
                }
                else
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\NaturalPoint\NATURALPOINT\NPClient Location"))
                        {
                            key.SetValue("Path", AppDomain.CurrentDomain.BaseDirectory);
                        }

                        bool msfsRunning = Process.GetProcessesByName("FlightSimulator").Any() || 
                                           Process.GetProcessesByName("FlightSimulator2024").Any();

                        string fixedText = "Head tracking was unconfigured, but we automatically applied a fix. You should be good to go going forward.";

                        if (msfsRunning)
                        {
                            TrackingStatusIcon.Fill = new SolidColorBrush(Colors.Orange);
                            TrackingStatusText.Text = fixedText;
                            TrackingRestartMsg.Visibility = Visibility.Visible;

                            Thread monitorThread = new Thread(() =>
                            {
                                while (Process.GetProcessesByName("FlightSimulator").Any() || Process.GetProcessesByName("FlightSimulator2024").Any())
                                {
                                    Thread.Sleep(2000);
                                }
                                CheckRegistryStatus();
                            }) { IsBackground = true };
                            monitorThread.Start();
                        }
                        else
                        {
                            TrackingStatusIcon.Fill = new SolidColorBrush(Colors.Green);
                            TrackingStatusText.Text = fixedText;
                            TrackingRestartMsg.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch
                    {
                        TrackingStatusIcon.Fill = new SolidColorBrush(Colors.Red);
                        TrackingStatusText.Text = "Head tracking does not appear to be correctly configured and we could not automatically fix it. Please check your registry permissions.";
                        TrackingRestartMsg.Visibility = Visibility.Collapsed;
                    }
                }
            });
        }
    }
}
