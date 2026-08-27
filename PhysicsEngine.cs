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

        private Random rand = new Random();

        private const double DT_MAX = 0.05;
        private const double SCALE_TRANS_XY = -3.0;
        private const double SCALE_TRANS_Z = 3.0;
        private const double SCALE_ROT = -0.05;
        
        private const double BUMP_AMP_BASE = 0.5;
        private const double BUMP_AMP_Y_MULT = 100.0;
        private const double BUMP_AMP_X_MULT = 50.0;
        private const double WHEEL_RPM_NORM = 1000.0;

        public void Update(SimConnectHandler.TelemetryData telemetry, double dt, double effectMultiplier = 1.0, double bumpMultiplier = 1.0)
        {
            if (dt <= 0) return;
            dt = Math.Min(dt, DT_MAX);

            // 1. Calculate external forces based on aircraft acceleration
            // It turns out FreeTrack/MSFS expects +Z for backward and -Z for forward.
            // When accelerating (+AccelZ), we want the head to move backward (+Z).
            // When braking (-AccelZ), we want the head to move forward (-Z).
            // Therefore, Z scale should be positive!
            
            double scaleX = SCALE_TRANS_XY * effectMultiplier; 
            double scaleY = SCALE_TRANS_XY * effectMultiplier; 
            double scaleZ = SCALE_TRANS_Z * effectMultiplier; 

            double extForceX = telemetry.AccelX * scaleX;
            double extForceY = telemetry.AccelY * scaleY;
            double extForceZ = telemetry.AccelZ * scaleZ;

            // Add runway bumpiness if on ground
            if (telemetry.SimOnGround == 1 && telemetry.WheelRpm > 10)
            {
                double bumpAmplitude = Math.Min(telemetry.WheelRpm / WHEEL_RPM_NORM, 1.0) * BUMP_AMP_BASE * effectMultiplier * bumpMultiplier;
                extForceY += (rand.NextDouble() - 0.5) * bumpAmplitude * BUMP_AMP_Y_MULT; 
                extForceX += (rand.NextDouble() - 0.5) * bumpAmplitude * BUMP_AMP_X_MULT;
            }

            // Update Spring-Mass-Damper for each axis
            UpdateAxis(ref HeadX, ref velX, extForceX, dt);
            
            // For Y-axis (vertical bumps), we want a much faster response (stiffer spring, lower mass effect)
            // so high frequency bumps from taxiing aren't completely swallowed by the low-pass filter effect.
            UpdateAxis(ref HeadY, ref velY, extForceY, dt, k: 500, c: 40, m: 2.0);
            
            UpdateAxis(ref HeadZ, ref velZ, extForceZ, dt);
            
            // Pitch and Roll based on X/Z accelerations (head tilts forward when braking)
            double extForcePitch = telemetry.AccelZ * SCALE_ROT * effectMultiplier;
            double extForceRoll = telemetry.AccelX * SCALE_ROT * effectMultiplier;

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
