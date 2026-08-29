using System;

namespace MsfsPhysicsCamera
{
    public class PhysicsEngine
    {
        // Spring-Mass-Damper for Head movement
        // F = m*a
        // F_spring = -k * x
        // F_damper = -c * v
        // m*a_head = F_ext + F_spring + F_damper

        private double headMass = 5.0; // kg
        private double springK = 200.0; // Spring constant
        private double damperC = 30.0; // Damping constant

        // Current state of head offset
        public double HeadX = 0;
        public double HeadY = 0;
        public double HeadZ = 0;
        public double HeadPitch = 0;
        public double HeadRoll = 0;

        private double velX = 0, velY = 0, velZ = 0;
        private double velPitch = 0, velRoll = 0;

        private const double DT_MAX = 0.05;
        private const double SCALE_TRANS_X = 3.0;
        private const double SCALE_TRANS_Y = -3.0;
        private const double SCALE_TRANS_Z = 3.0;
        private const double SCALE_ROT_PITCH = -0.05;
        private const double SCALE_ROT_ROLL = 0.05;

        public void Update(SimConnectHandler.TelemetryData telemetry, double dt, double effectMultiplier = 1.0)
        {
            if (dt <= 0) return;
            dt = Math.Min(dt, DT_MAX);

            // 1. Calculate external forces based on aircraft acceleration
            // It turns out FreeTrack/MSFS expects +Z for backward and -Z for forward.
            // When accelerating (+AccelZ), we want the head to move backward (+Z).
            // When braking (-AccelZ), we want the head to move forward (-Z).
            // Therefore, Z scale should be positive!
            
            double scaleX = SCALE_TRANS_X * effectMultiplier; 
            double scaleY = SCALE_TRANS_Y * effectMultiplier; 
            double scaleZ = SCALE_TRANS_Z * effectMultiplier; 

            double extForceX = telemetry.AccelX * scaleX;
            double extForceY = telemetry.AccelY * scaleY;
            double extForceZ = telemetry.AccelZ * scaleZ;

            // Update Spring-Mass-Damper for each axis
            UpdateAxis(ref HeadX, ref velX, extForceX, dt);
            
            // For Y-axis (vertical bumps), we want a much faster response (stiffer spring, lower mass effect)
            // so high frequency bumps from taxiing aren't completely swallowed by the low-pass filter effect.
            UpdateAxis(ref HeadY, ref velY, extForceY, dt, k: 500, c: 40, m: 2.0);
            
            UpdateAxis(ref HeadZ, ref velZ, extForceZ, dt);
            
            // Pitch and Roll based on X/Z accelerations (head tilts forward when braking)
            double extForcePitch = telemetry.AccelZ * SCALE_ROT_PITCH * effectMultiplier;
            double extForceRoll = telemetry.AccelX * SCALE_ROT_ROLL * effectMultiplier;

            UpdateAxis(ref HeadPitch, ref velPitch, extForcePitch, dt, k: 300, c: 40);
            UpdateAxis(ref HeadRoll, ref velRoll, extForceRoll, dt, k: 300, c: 40);
        }

        private void UpdateAxis(ref double pos, ref double vel, double extForce, double dt, double? k = null, double? c = null, double? m = null)
        {
            double actualK = k ?? springK;
            double actualC = c ?? damperC;
            double actualM = m ?? headMass;

            double forceSpring = -actualK * pos;
            double forceDamper = -actualC * vel;
            double totalForce = extForce + forceSpring + forceDamper;

            double accel = totalForce / actualM;
            
            vel += accel * dt;
            pos += vel * dt;
        }
    }
}
