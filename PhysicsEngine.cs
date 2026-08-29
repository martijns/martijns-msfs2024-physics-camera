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

        // Noise position advances with wheel rotation, so bump frequency scales with taxi speed.
        private double noisePos = 0;

        private const double DT_MAX = 0.05;
        private const double SCALE_TRANS_X = 3.0;
        private const double SCALE_TRANS_Y = -3.0;
        private const double SCALE_TRANS_Z = 3.0;
        private const double SCALE_ROT_PITCH = -0.05;
        private const double SCALE_ROT_ROLL = 0.05;
        
        private const double BUMP_AMP_BASE = 0.5;
        private const double BUMP_AMP_Y_MULT = 25.0;
        private const double BUMP_AMP_X_MULT = 12.5;
        private const double WHEEL_RPM_NORM = 1000.0;

        // Pavement features per wheel revolution. At 1000 RPM this yields a base bump
        // frequency of ~10 Hz (well above the Y-axis natural frequency of ~2.5 Hz)
        // and a detail octave at ~25 Hz (below the ~30 Hz Nyquist of the 60 Hz loop).
        private const double BUMP_TEXTURE_SCALE = 0.6;
        private const int BUMP_SEED_Y = 1;
        private const int BUMP_SEED_X = 7;

        public void Update(SimConnectHandler.TelemetryData telemetry, double dt, double effectMultiplier = 1.0, double bumpMultiplier = 1.0)
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

            // Add runway bumpiness if on ground.
            // Uses Perlin noise sampled along a position driven by wheel rotation, so the
            // bump frequency scales with taxi speed. Two octaves (base + detail) keep the
            // texture crisp instead of feeling dampened by the spring-damper low-pass.
            if (telemetry.SimOnGround == 1 && telemetry.WheelRpm > 10)
            {
                double revPerSec = telemetry.WheelRpm / 60.0;
                noisePos += revPerSec * BUMP_TEXTURE_SCALE * dt;

                double bumpAmplitude = Math.Min(telemetry.WheelRpm / WHEEL_RPM_NORM, 1.0) * BUMP_AMP_BASE * effectMultiplier * bumpMultiplier;

                // Base octave (pavement slabs) + detail octave at 2.5x frequency (surface texture)
                double noiseY = Perlin1D(noisePos + BUMP_SEED_Y) + 0.5 * Perlin1D(noisePos * 2.5 + BUMP_SEED_Y + 100);
                double noiseX = Perlin1D(noisePos + BUMP_SEED_X) + 0.5 * Perlin1D(noisePos * 2.5 + BUMP_SEED_X + 100);

                extForceY += noiseY * bumpAmplitude * BUMP_AMP_Y_MULT;
                extForceX += noiseX * bumpAmplitude * BUMP_AMP_X_MULT;
            }

            // Update Spring-Mass-Damper for each axis
            UpdateAxis(ref HeadX, ref velX, extForceX, dt);
            
            // For Y-axis (vertical bumps), we want a much faster response (stiffer spring, lower mass effect)
            // so high frequency bumps from taxiing aren't completely swallowed by the low-pass filter effect.
            // UpdateAxis(ref HeadY, ref velY, extForceY, dt, k: 500, c: 40, m: 2.0);
            UpdateAxis(ref HeadY, ref velY, extForceY, dt);
            
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

        /// <summary>
        /// 1D Perlin (gradient) noise. Returns a smooth, continuous value in roughly [-1, 1].
        /// Uses a deterministic hash-based gradient so no lookup table is needed.
        /// </summary>
        private static double Perlin1D(double x)
        {
            int x0 = (int)Math.Floor(x);
            double fx = x - x0;

            double g0 = Gradient(x0);
            double g1 = Gradient(x0 + 1);

            double d0 = g0 * fx;
            double d1 = g1 * (fx - 1.0);

            double t = Fade(fx);
            return Lerp(d0, d1, t) * 2.0; // scale to roughly [-1, 1]
        }

        private static double Fade(double t)
        {
            // 6t^5 - 15t^4 + 10t^3
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + t * (b - a);
        }

        private static double Gradient(int hash)
        {
            // Deterministic pseudo-random gradient in [-1, 1] from integer coordinate
            uint h = (uint)hash;
            h ^= h >> 16;
            h *= 0x7feb352d;
            h ^= h >> 15;
            h *= 0x846ca68b;
            h ^= h >> 16;
            return (h / (double)uint.MaxValue) * 2.0 - 1.0;
        }
    }
}
