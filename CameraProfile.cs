using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MsfsPhysicsCamera
{
    public class PhysicsProfileSettings
    {
        public double EffectMultiplier { get; set; } = 1.0;
        public double TransXMultiplier { get; set; } = 1.0;
        public double TransYMultiplier { get; set; } = 1.0;
        public double TransZMultiplier { get; set; } = 1.0;
        public double PitchMultiplier { get; set; } = 1.0;
        public double RollMultiplier { get; set; } = 1.0;

        public double TransXMin { get; set; } = -1.0;
        public double TransXMax { get; set; } = 1.0;
        public double TransYMin { get; set; } = -1.0;
        public double TransYMax { get; set; } = 1.0;
        public double TransZMin { get; set; } = -1.0;
        public double TransZMax { get; set; } = 1.0;
        public double PitchMin { get; set; } = -0.05;
        public double PitchMax { get; set; } = 0.05;
        public double RollMin { get; set; } = -0.05;
        public double RollMax { get; set; } = 0.05;

        public double WashoutStrength { get; set; } = 0.5;

        public bool SimulateGravity { get; set; } = true;
        public bool OnlyInCockpit { get; set; } = true;

        public PhysicsProfileSettings Clone()
        {
            return (PhysicsProfileSettings)MemberwiseClone();
        }

        public bool Matches(PhysicsProfileSettings other)
        {
            if (other == null) return false;

            const double eps = 1e-4;
            return Math.Abs(EffectMultiplier - other.EffectMultiplier) < eps
                && Math.Abs(TransXMultiplier - other.TransXMultiplier) < eps
                && Math.Abs(TransYMultiplier - other.TransYMultiplier) < eps
                && Math.Abs(TransZMultiplier - other.TransZMultiplier) < eps
                && Math.Abs(PitchMultiplier - other.PitchMultiplier) < eps
                && Math.Abs(RollMultiplier - other.RollMultiplier) < eps
                && Math.Abs(TransXMin - other.TransXMin) < eps
                && Math.Abs(TransXMax - other.TransXMax) < eps
                && Math.Abs(TransYMin - other.TransYMin) < eps
                && Math.Abs(TransYMax - other.TransYMax) < eps
                && Math.Abs(TransZMin - other.TransZMin) < eps
                && Math.Abs(TransZMax - other.TransZMax) < eps
                && Math.Abs(PitchMin - other.PitchMin) < eps
                && Math.Abs(PitchMax - other.PitchMax) < eps
                && Math.Abs(RollMin - other.RollMin) < eps
                && Math.Abs(RollMax - other.RollMax) < eps
                && Math.Abs(WashoutStrength - other.WashoutStrength) < eps
                && SimulateGravity == other.SimulateGravity
                && OnlyInCockpit == other.OnlyInCockpit;
        }

        public string ToDisplaySummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Global Multiplier: {EffectMultiplier:F2}x");
            sb.AppendLine($"Trans Multipliers (X/Y/Z): {TransXMultiplier:F2}x / {TransYMultiplier:F2}x / {TransZMultiplier:F2}x");
            sb.AppendLine($"Rot Multipliers (Pitch/Roll): {PitchMultiplier:F2}x / {RollMultiplier:F2}x");
            sb.AppendLine($"Trans X Clamp: {TransXMin:F2} to {TransXMax:F2}");
            sb.AppendLine($"Trans Y Clamp: {TransYMin:F2} to {TransYMax:F2}");
            sb.AppendLine($"Trans Z Clamp: {TransZMin:F2} to {TransZMax:F2}");
            sb.AppendLine($"Pitch Clamp: {PitchMin:F3} to {PitchMax:F3}");
            sb.AppendLine($"Roll Clamp: {RollMin:F3} to {RollMax:F3}");
            sb.AppendLine($"Washout Filter: {WashoutStrength:F2}");
            sb.AppendLine($"Simulate Gravity: {(SimulateGravity ? "Yes" : "No")}");
            sb.Append($"Only Active in Cockpit: {(OnlyInCockpit ? "Yes" : "No")}");
            return sb.ToString();
        }
    }

    public class CameraProfile
    {
        public const string DefaultProfileName = "Default";
        public const string CustomProfileName = "Custom";
        public const string SharePrefix = "MSFSCAM";
        public const int CurrentFormatVersion = 1;
        public const int MaxSharePayloadBytes = 64 * 1024;

        public string Name { get; set; } = string.Empty;
        public PhysicsProfileSettings Settings { get; set; } = new();

        public static CameraProfile CreateDefault()
        {
            return new CameraProfile
            {
                Name = DefaultProfileName,
                Settings = new PhysicsProfileSettings()
            };
        }

        private class SharePayload
        {
            public int V { get; set; } = CurrentFormatVersion;
            public string Name { get; set; } = string.Empty;
            public PhysicsProfileSettings Settings { get; set; } = new();
        }

        private static readonly JsonSerializerOptions ShareJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public string ToShareString()
        {
            var payload = new SharePayload
            {
                V = CurrentFormatVersion,
                Name = Name,
                Settings = Settings
            };

            string json = JsonSerializer.Serialize(payload, ShareJsonOptions);
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(json);

            using var outputStream = new MemoryStream();
            using (var gzip = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(utf8Bytes, 0, utf8Bytes.Length);
            }

            return SharePrefix + Convert.ToBase64String(outputStream.ToArray());
        }

        public static bool TryFromShareString(string? input, out CameraProfile? profile, out string? error)
        {
            profile = null;
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Please enter a share string.";
                return false;
            }

            string trimmed = input.Trim();
            if (!trimmed.StartsWith(SharePrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Share string must start with '{SharePrefix}'.";
                return false;
            }

            string base64Part = trimmed.Substring(SharePrefix.Length).Trim();
            if (string.IsNullOrEmpty(base64Part))
            {
                error = "Share string is missing encoded profile data.";
                return false;
            }

            byte[] rawBytes;
            try
            {
                rawBytes = Convert.FromBase64String(base64Part);
            }
            catch (FormatException)
            {
                error = "Invalid Base64 data in share string.";
                return false;
            }

            if (rawBytes.Length > MaxSharePayloadBytes)
            {
                error = "Share string exceeds maximum allowed size.";
                return false;
            }

            string json;
            try
            {
                // Support both GZip-compressed payloads and plain UTF-8 JSON payloads
                if (rawBytes.Length >= 2 && rawBytes[0] == 0x1F && rawBytes[1] == 0x8B)
                {
                    using var inputStream = new MemoryStream(rawBytes);
                    using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
                    using var decompressed = new MemoryStream();
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (decompressed.Length + bytesRead > MaxSharePayloadBytes)
                        {
                            error = "Share string exceeds maximum allowed size.";
                            return false;
                        }
                        decompressed.Write(buffer, 0, bytesRead);
                    }
                    json = Encoding.UTF8.GetString(decompressed.ToArray());
                }
                else
                {
                    json = Encoding.UTF8.GetString(rawBytes);
                }
            }
            catch (Exception)
            {
                error = "Unable to decompress profile data.";
                return false;
            }

            try
            {
                var payload = JsonSerializer.Deserialize<SharePayload>(json, ShareJsonOptions);
                if (payload == null || payload.Settings == null)
                {
                    error = "Share string does not contain valid profile settings.";
                    return false;
                }

                if (payload.V < 1)
                {
                    error = "Share string has an invalid format version.";
                    return false;
                }

                if (payload.V > CurrentFormatVersion)
                {
                    error = $"This profile was created with a newer format version (v{payload.V}, supported up to v{CurrentFormatVersion}). Please update MSFS Physics Camera to import it.";
                    return false;
                }

                string name = string.IsNullOrWhiteSpace(payload.Name)
                    ? "Imported Profile"
                    : payload.Name.Trim();

                profile = new CameraProfile
                {
                    Name = name,
                    Settings = payload.Settings
                };
                return true;
            }
            catch (JsonException)
            {
                error = "Share string contains malformed profile JSON.";
                return false;
            }
        }
    }
}
