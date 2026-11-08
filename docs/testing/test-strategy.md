# Test Strategy & Traceability Matrix: Time Chime

## 1. Testing Philosophy
- **Deterministic Pure Logic**: The core scheduling calculation (`ScheduleCalculator`), time-slot mapping (`TimeSlotMapper`), phrase formatting (`AnnouncementFormatter`), and PCM signal processing (`AudioPcmProcessor`) are isolated from OS clocks and hardware devices for deterministic unit testing.
- **Fast Feedback Loop**: All 22 automated xUnit tests execute in under 1 second via `dotnet test`.

## 2. Requirements Traceability Matrix

| Req ID | Requirement Description | Verification Type | Acceptance Criteria |
|---|---|---|---|
| **FR-01** | Master enable/disable | Unit / UI | When `Enabled=false`, `ScheduleCalculator.GetNextChime()` returns `null`. UI status shows 'Disabled'. |
| **FR-02** | Frequency (60, 30, 15 min) | Unit | Aligns strictly to wall-clock minute boundaries: `:00`, `:15`, `:30`, `:45`. |
| **FR-03** | Active window (start/end) | Unit | Start time inclusive; end time exclusive. E.g. `09:00–18:00` announces at `09:00` and `17:45`, not `18:00`. |
| **FR-04** | Overnight schedule | Unit | `22:00–06:00` window starting Friday permits Saturday `02:00` announcement as part of Friday's overnight window. |
| **FR-05** | Weekdays selection | Unit / UI | Monday–Sunday individually toggled with quick presets. |
| **FR-06** | Weekend off shortcut | Unit / UI | Toggling off weekends deselects Saturday and Sunday; no announcements fire on Saturday/Sunday starts. |
| **FR-07** | Voice library (8 neural voices) | Unit / Integration | 6 English (`af_heart`, `af_bella`, `bf_emma`, `am_michael`, `am_eric`, `bm_george`) and 2 Hindi (`hf_alpha`, `hm_omega`) offline Kokoro-82M voices. |
| **FR-08** | Voice selection persistence | Unit / Integration | Selected voice ID persists across restarts in `settings.json`. |
| **FR-09** | Voice preview | UI / Integration | Preview button cycles through 4 representative time samples per voice; clicking again stops playback immediately. |
| **FR-10** | Speech content format | Unit | English uses 12h `"It's..."` without AM/PM (48 unique slots); Hindi uses natural Devanagari with time-of-day periods (96 unique slots). |
| **FR-11** | System tray | Integration | Minimize-to-tray, native Win32 context menu (Open Settings, Pause/Resume, Chime Now, Exit). |
| **FR-12** | Startup at login | Integration | Supports both User-Level (`HKCU\Run`) and System-Level (`HKLM\Run` + All-Users Common Startup) with `--minimized` launch. |
| **FR-13** | Local settings JSON | Unit / Integration | Atomic file write via temp swap, automatic `.bak` backup recovery on corruption. |
| **FR-14** | Offline guarantee | Architecture | Zero outbound HTTP sockets; all 480 audio clips embedded locally. |
| **FR-15** | Volume & Speed controls | Unit / UI | `AudioPcmProcessor` scales 16-bit PCM amplitude (`0%–100%`) and resamples tempo (`0.5x–2.0x`) while keeping a fixed 24 kHz WAV header. |
| **FR-16** | Optional chime bell | Integration | Pre-roll multi-harmonic acoustic bell (`880 Hz`, `1760 Hz`, `2640 Hz`) synthesized in memory before voice playback. |
| **FR-17** | Pause & Resume | UI / Integration | Instant pause toggle from main window or system tray context menu. |
| **FR-18** | Single instance | Integration | Named Win32 mutex prevents duplicate instances. |
| **FR-19** | System-level installer | Packaging QA | Self-contained `TimeChime-Setup.exe` installs to `C:\Program Files\Time Chime` with UAC elevation and Uninstall registry support. |
