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

        public void Update(SimConnectHandler.TelemetryData telemetry, double dt, double effectMultiplier = 1.0, double bumpMultiplier = 1.0)
        {
            if (dt <= 0) return;

            // 1. Calculate external forces based on aircraft acceleration
            // It turns out FreeTrack/MSFS expects +Z for backward and -Z for forward.
            // When accelerating (+AccelZ), we want the head to move backward (+Z).
            // When braking (-AccelZ), we want the head to move forward (-Z).
            // Therefore, Z scale should be positive!
            
            double scaleX = -3.0 * effectMultiplier; 
            double scaleY = -3.0 * effectMultiplier; 
            double scaleZ = 3.0 * effectMultiplier; // Inverted!

            double extForceX = telemetry.AccelX * scaleX;
            double extForceY = telemetry.AccelY * scaleY;
            double extForceZ = telemetry.AccelZ * scaleZ;

            // Add runway bumpiness if on ground
            if (telemetry.SimOnGround == 1 && telemetry.WheelRpm > 10)
            {
                double bumpAmplitude = Math.Min(telemetry.WheelRpm / 1000.0, 1.0) * 0.5 * effectMultiplier * bumpMultiplier;
                extForceY += (rand.NextDouble() - 0.5) * bumpAmplitude * 100.0; 
                extForceX += (rand.NextDouble() - 0.5) * bumpAmplitude * 50.0;
            }

            // Update Spring-Mass-Damper for each axis
            UpdateAxis(ref HeadX, ref velX, extForceX, dt);
            
            // For Y-axis (vertical bumps), we want a much faster response (stiffer spring, lower mass effect)
            // so high frequency bumps from taxiing aren't completely swallowed by the low-pass filter effect.
            UpdateAxis(ref HeadY, ref velY, extForceY, dt, k: 500, c: 40, m: 2.0);
            
            UpdateAxis(ref HeadZ, ref velZ, extForceZ, dt);
            
            // Pitch and Roll based on X/Z accelerations (head tilts forward when braking)
            double extForcePitch = telemetry.AccelZ * -0.05 * effectMultiplier;
            double extForceRoll = telemetry.AccelX * -0.05 * effectMultiplier;

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
