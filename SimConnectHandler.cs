using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.FlightSimulator.SimConnect;

namespace MsfsPhysicsCamera
{
    public class SimConnectHandler : IDisposable
    {
        private const int WM_USER_SIMCONNECT = 0x0402;
        private SimConnect? simconnect = null;
        private volatile bool _isConnected = false;
        private readonly object _dataLock = new object();
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Thread? _messageThread;

        public enum DEFINITIONS { TelemetryData, AircraftInfo }
        public enum DATA_REQUESTS { TelemetryRequest, AircraftInfoRequest }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TelemetryData
        {
            public double AccelX;
            public double AccelY;
            public double AccelZ;
            public double VelX;
            public double VelY;
            public double VelZ;
            public double Pitch;
            public double Bank;
            public int CameraState;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AircraftInfo
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Title;
        }

        private TelemetryData _currentData;
        private string _currentAircraftTitle = string.Empty;

        public TelemetryData CurrentData
        {
            get
            {
                lock (_dataLock)
                {
                    return _currentData;
                }
            }
        }

        public string CurrentAircraftTitle
        {
            get
            {
                lock (_dataLock)
                {
                    return _currentAircraftTitle;
                }
            }
        }

        public bool IsConnected => _isConnected;

        public void Connect()
        {
            if (_isConnected) return;

            // Clean up any previous connection before reconnecting
            _cts.Cancel();
            _messageThread?.Join(500);
            simconnect?.Dispose();
            simconnect = null;
            _cts = new CancellationTokenSource();

            try
            {
                simconnect = new SimConnect("MsfsPhysicsCamera", IntPtr.Zero, WM_USER_SIMCONNECT, null, 0);
                
                // Define SimVars
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "ACCELERATION BODY X", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "ACCELERATION BODY Y", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "ACCELERATION BODY Z", "feet per second squared", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "VELOCITY BODY X", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "VELOCITY BODY Y", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "VELOCITY BODY Z", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "PLANE PITCH DEGREES", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "PLANE BANK DEGREES", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.TelemetryData, "CAMERA STATE", "enum", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.RegisterDataDefineStruct<TelemetryData>(DEFINITIONS.TelemetryData);

                simconnect.AddToDataDefinition(DEFINITIONS.AircraftInfo, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.RegisterDataDefineStruct<AircraftInfo>(DEFINITIONS.AircraftInfo);

                simconnect.OnRecvOpen += Simconnect_OnRecvOpen;
                simconnect.OnRecvQuit += Simconnect_OnRecvQuit;
                simconnect.OnRecvException += Simconnect_OnRecvException;
                simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;

                // Request data every frame
                simconnect.RequestDataOnSimObject(DATA_REQUESTS.TelemetryRequest, DEFINITIONS.TelemetryData, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SIM_FRAME, 0, 0, 0, 0);
                // Request aircraft title when it changes
                simconnect.RequestDataOnSimObject(DATA_REQUESTS.AircraftInfoRequest, DEFINITIONS.AircraftInfo, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.CHANGED, 0, 0, 0);

                Console.WriteLine("Connected to MSFS.");
                _isConnected = true;

                // Start a thread to process SimConnect messages
                var token = _cts.Token;
                var sc = simconnect;
                _messageThread = new Thread(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            sc?.ReceiveMessage();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("SimConnect ReceiveMessage Error: " + ex.Message);
                            _isConnected = false;
                            break;
                        }
                        Thread.Sleep(1);
                    }
                })
                { IsBackground = true };
                _messageThread.Start();
            }
            catch (COMException ex)
            {
                Console.WriteLine($"SimConnect Connection Failed: {ex.Message}");
                _isConnected = false;
                simconnect?.Dispose();
                simconnect = null;
            }
        }

        private void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if (data.dwRequestID == (uint)DATA_REQUESTS.TelemetryRequest)
            {
                lock (_dataLock)
                {
                    _currentData = (TelemetryData)data.dwData[0];
                }
            }
            else if (data.dwRequestID == (uint)DATA_REQUESTS.AircraftInfoRequest)
            {
                var info = (AircraftInfo)data.dwData[0];
                lock (_dataLock)
                {
                    _currentAircraftTitle = info.Title?.Trim() ?? string.Empty;
                }
            }
        }

        private void Simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Console.WriteLine($"SimConnect Exception: {data.dwException}");
        }

        private void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("MSFS exited. Disconnecting...");
            _isConnected = false;
            lock (_dataLock)
            {
                _currentAircraftTitle = string.Empty;
            }
            _cts.Cancel();
        }

        private void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("SimConnect Open event received.");
            _isConnected = true;
        }

        public void Dispose()
        {
            _isConnected = false;
            _cts.Cancel();
            _messageThread?.Join(500);

            if (simconnect != null)
            {
                simconnect.Dispose();
                simconnect = null;
            }
        }
    }
}
