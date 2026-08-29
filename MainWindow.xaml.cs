using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media;

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

        private AppSettings settings;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            settings = AppSettings.Load();
            _effectMultiplier = settings.EffectMultiplier;

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
                        physics.Update(data, dt, EffectMultiplier);

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
    }
}
