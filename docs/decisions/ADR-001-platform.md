# ADR-001: Desktop Framework, Target Runtime and Solution Structure

## Context
Time Chime requires a lightweight, single-process, offline Windows 10/11 utility. It must support high-DPI displays (100%–200%), dark/light system theming, keyboard accessibility, low memory usage, system tray integration, and zero external runtime dependencies beyond .NET. Web technologies (Electron, WebView2) were rejected due to excessive memory footprint (200MB+ idle) and cold-start latency. Windows Forms was rejected due to poor high-DPI vector scaling and theming maintenance.

## Decision
1. **Runtime & Language**: C# 13 on .NET 10.0 (`net10.0-windows`). Nullable reference types enabled, strict compiler warnings, zero warnings baseline.
2. **UI Framework**: Windows Presentation Foundation (WPF) with modern Fluent styling and custom vector icon resources. WPF is mature, natively integrated into Windows, starts in under 1 second, and has minimal memory overhead.
3. **Solution Organization**:
   - `TimeChime.Core`: `net10.0` class library with pure domain logic (`ScheduleCalculator`, `AnnouncementFormatter`, `TimeSlotMapper`, `AudioPcmProcessor`, contracts, settings models). Zero UI or OS-specific dependencies.
   - `TimeChime.Infrastructure`: Persistence (atomic JSON with backup/recovery).
   - `TimeChime.App`: WPF executable containing XAML views, ViewModels, Theme managers, audio playback (`SpeechAudioService`), startup registration (`StartupManager`), and native Win32 tray integration (`SystemTrayManager`).
   - `TimeChime.Core.Tests`: Automated xUnit test suite targeting pure scheduling rules, boundary conditions, formatting, and PCM audio processing.

## Consequences
- **Positive**: Fast startup, rock-solid stability, zero web engine bloat, 100% offline execution.
- **Negative**: Dark/light theming in WPF requires deliberate theme dictionary management (implemented via DynamicResources and application palette dictionaries).
