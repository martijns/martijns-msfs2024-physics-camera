using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace MsfsPhysicsCamera
{
    public class FreeTrackInjector : IDisposable
    {
        private const int CAM_WIDTH = 640;
        private const int CAM_HEIGHT = 480;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FreeTrackData
        {
            public int DataID;
            public int CamWidth;
            public int CamHeight;
            public float Yaw;
            public float Pitch;
            public float Roll;
            public float X;
            public float Y;
            public float Z;
            public float RawYaw;
            public float RawPitch;
            public float RawRoll;
            public float RawX;
            public float RawY;
            public float RawZ;
            public float x1, y1, x2, y2, x3, y3, x4, y4;
        }

        private MemoryMappedFile? mmf;
        private MemoryMappedViewAccessor? accessor;
        private int dataId = 0;

        public FreeTrackInjector()
        {
            TryInitialize();
        }

        private void TryInitialize()
        {
            try
            {
                if (accessor == null)
                {
                    mmf?.Dispose();
                    mmf = MemoryMappedFile.CreateOrOpen("FT_SharedMem", Marshal.SizeOf(typeof(FreeTrackData)));
                    accessor = mmf.CreateViewAccessor();
                    Console.WriteLine("FreeTrack Memory Mapped File initialized.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize FreeTrack memory: {ex.Message}");
            }
        }

        public void Update(float x, float y, float z, float pitch, float roll, float yaw)
        {
            if (accessor == null)
            {
                TryInitialize();
                if (accessor == null) return;
            }

            var data = new FreeTrackData
            {
                DataID = Interlocked.Increment(ref dataId),
                CamWidth = CAM_WIDTH,
                CamHeight = CAM_HEIGHT,
                X = x,
                Y = y,
                Z = z,
                Pitch = pitch,
                Roll = roll,
                Yaw = yaw,
                RawX = x,
                RawY = y,
                RawZ = z,
                RawPitch = pitch,
                RawRoll = roll,
                RawYaw = yaw
            };

            accessor.Write(0, ref data);
        }

        public void Dispose()
        {
            accessor?.Dispose();
            mmf?.Dispose();
        }
    }
}
