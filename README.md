# Visma Software Nordic Screensaver

A Windows screensaver (.scr) that animates embedded images (compiled from your repo `images/` folder) with smooth Ken Burns, pan, rotate, parallax, cross-zoom, and tiles effects.

## Prerequisites

- Run (end users):
  - Windows 10 or 11 (x64)
  - .NET Desktop Runtime 6.0 (x64) installed (shows as `Microsoft.WindowsDesktop.App 6.0.x`)
    - Verify: `dotnet --list-runtimes` and look for `Microsoft.WindowsDesktop.App 6.0.x`
- Build (developers):
  - .NET SDK 6.0 (x64)
  - PowerShell (to run `build.ps1`)
  - Optional: Visual Studio 2022 with ".NET desktop development" workload

### Download Links

- .NET 6 Desktop Runtime (Windows x64): https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime
- .NET 6 SDK (Windows x64): https://dotnet.microsoft.com/en-us/download/dotnet/6.0

### Verify Install

```powershell
dotnet --list-runtimes   # should include Microsoft.WindowsDesktop.App 6.0.x
dotnet --list-sdks       # should include 6.0.x (if building)
```

## Quick Start

- Prerequisites: .NET Desktop Runtime 6.0 on Windows (for running) or .NET SDK 6.0 (for building)
- Images: place your pictures in the workspace `images/` folder (they will be embedded at build time)

### Build

```powershell
cd "CreativeScreensaver"
./build.ps1
```

Outputs: `dist/VismaSoftwareNordic.scr` (single file)

### Install

- Option A (quick test): Double-click `VismaSoftwareNordic.scr` then choose `Preview`.
- Option B (system install):
  1. Copy `dist/VismaSoftwareNordic.scr` to `%WINDIR%\System32` (admin may be required)
  2. Open Windows Screen Saver Settings, select "VismaSoftwareNordic"

Note: The build now produces a single-file framework-dependent `.scr`. No additional files are required; keep just `VismaSoftwareNordic.scr`.

### Configure

- Run: `VismaSoftwareNordic.scr /c` or click `Settings` in Screen Saver settings.
- Options:
  - **Slide duration**: How long each image displays (seconds)
  - **Transition speed**: Fade in/out duration (seconds)
  - **Animation style**: Random, KenBurns, Pan, Rotate, Parallax, CrossZoom, Tiles
  - **Animation intensity**: 0-100 scale (0 = subtle, 50 = balanced, 100 = dramatic movement)
  - **Randomize order**: Shuffle images

## Features

- **Embedded-only images**: Images from `images/` are compiled into the .scr for a locked build
- **Seven animation styles**: Ken Burns, Pan, Rotate, Parallax, CrossZoom, Tiles, plus Random
- **Adjustable intensity**: Control movement range and speed (0-100)
- **Multi-monitor**: One fullscreen window per display
- **Particle overlay**: Subtle particles for depth and motion
- **Image formats**: .jpg, .jpeg, .png, .bmp, .gif
- **Image sizing**: Images are decoded at max 1920×1280 (no upscaling of smaller sources)
- **Exit controls**: Mouse move/click or any key; Preview mode (`/p`) supported

## Adding Your Own Images

This build is embedded-only. To include or change images:
1. Place your `.png`, `.jpg`, `.jpeg`, `.bmp`, or `.gif` files in the `images/` folder
2. Rebuild: `cd CreativeScreensaver; ./build.ps1`
3. The produced `.scr` will include the updated images

## Developing

Run in windowed mode via Visual Studio or `dotnet run`. For screen saver behavior, use the produced `.scr`.

```powershell
# Rebuild from scratch
cd "CreativeScreensaver"
./build.ps1 -Clean
```

## macOS (Experimental)

An initial macOS ScreenSaver bundle is included. It displays images from the repo `images/` folder (copied into the bundle at build time) and supports a preferences sheet with slide duration, transition, display scale, intensity, shuffle, clock (with font family/size), and basic animation styles (Crossfade, Ken Burns).

**Note**: Building the macOS screensaver requires Xcode on macOS. Cross-compilation from Windows is not supported.

### Build

```bash
open macos/VismaSoftwareNordicSaver/VismaSoftwareNordicSaver.xcodeproj
```

- Select the `VismaSoftwareNordicSaver` scheme and build (Debug or Release).
- Output: `build/Release/VismaSoftwareNordicSaver.saver` (path may vary by Xcode version).

### Install

```bash
mkdir -p ~/Library/"Screen Savers"
cp -R build/Release/VismaSoftwareNordicSaver.saver ~/Library/"Screen Savers"/
```

Open System Settings → Screen Saver and choose “VismaSoftwareNordicSaver”. Click “Options…” to open the preferences.

### Fonts

- **FiraMono Nerd Font** (Regular, Medium, Bold) is already included in `macos/VismaSoftwareNordicSaver/` and will be bundled automatically.
- The build copies these fonts into the bundle at `Resources/fonts/`; the saver registers them at runtime.
- In Options, set "Clock Font Family" to `FiraMono Nerd Font` (or add your own `.ttf`/`.otf` files to that folder).
- If the font isn't found, it falls back to a monospaced system font.

### Notes

- Images are copied from the repo `images/` folder into the saver bundle at build time.
- The preferences are stored via `ScreenSaverDefaults` and applied immediately when saved.
- This macOS project is experimental and may evolve; animation parity with Windows is partial by design.