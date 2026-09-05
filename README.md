# MSFS Physics Camera

![AI Assisted](https://img.shields.io/badge/AI-assisted-blue)
![MIT](https://img.shields.io/github/license/martijns/martijns-msfs2024-physics-camera)

A standalone application for Microsoft Flight Simulator 2024 that adds realistic, physics-based head movements (head shake, G-force effects) to the virtual cockpit. It reads aircraft telemetry via SimConnect and injects camera movements using the FreeTrack protocol.

## Features

- **Physics-Based Movement**: Uses a spring-mass-damper physics engine to simulate realistic head movements based on aircraft acceleration and velocity.
- **Customizable Effects**: Adjust the intensity of lateral, vertical, longitudinal, pitch, and roll movements in real-time via the UI.
- **Seamless Integration**: Connects to MSFS via SimConnect and injects data via FreeTrack.
- **Auto-Start**: Includes scripts to automatically start and stop with MSFS.

## Showcase

![MSFS Physics Camera](.github/ss1.png)
![Turbulence](.github/turbulence.mp4)
![Bouncy landing](.github/bouncy_landing.mp4)
![Applying full brake during taxi](.github/apply_full_brake_during_taxi.mp4)

## Compatibility

Verified to work on:
- Microsoft Flight Simulator 2024

*Note: TrackIR/FreeTrack must be enabled in MSFS for the camera injection to work.*

## Installation

1. Download the latest release.
2. Extract the files to your MSFS Community folder (or any other folder of your choice).
3. Run `AutoStart-Install.bat` to register the application to start automatically when MSFS launches.
   - *To uninstall the auto-start behavior, run `AutoStart-Uninstall.bat`.*
4. Alternatively, you can run `MsfsPhysicsCamera.exe` manually before or during your flight.
5. You'll likely want to disable the built-in camera shake ![MSFS Disable Camera Shake](.github/msfs_disable_camera_shake.png)

## Development

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Microsoft Flight Simulator 2024 SDK (required for `SimConnect.dll`)

### Setup

```bash
# Clone the repository
git clone https://github.com/martijns/martijns-msfs2024-physics-camera.git
cd martijns-msfs2024-physics-camera

# Build the project
dotnet build
```

*Note: The project expects `SimConnect.dll` to be located at `c:\MSFS 2024 SDK\SimConnect SDK\lib\SimConnect.dll`.*

### Debugging

You can run and debug the application directly from Visual Studio or VS Code. The application will attempt to connect to SimConnect and the FreeTrack shared memory automatically.

## License

MIT License - see LICENSE file for details.
