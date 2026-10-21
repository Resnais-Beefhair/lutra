# Preview validation

## Local GPU-driver recovery fix — 2026-10-21

See the [GPU driver recovery report](validation/2026-10-21-gpu-driver-recovery.md) for the observed post-update GPU-temperature loss, correct pump fallback and recovery. Local build 0.6.1-local.1 adds bounded GPU rediscovery, a persisted process-restart budget and a protected manual command. Tests cover permanent loss, invalid data, sensor identity and real high-temperature safety. A separate read-only fault simulation and live GPU-only command were checked; the actual driver installation was not repeated and the clean-process escalation was not forced against production cooling. Configuration and raw QUADRO settings stayed unchanged.

## Local CPU_FAN commissioning — 2026-10-19

See the [ASRock CPU_FAN1 report](validation/2026-10-19-asrock-cpu-fan.md) for a local build using the corrected LibreHardwareMonitor prerelease. A Ryzen 7 7800X3D temperature curve drove fan speed from approximately 509 to 1256 RPM and back to 509 RPM, with strict 30% minimum recovery. The existing QUADRO curves, four alarms and complete controller settings were preserved. Production safety fallback and firmware-mode restoration were checked. The first cold handback check failed its rotating-fan condition; a separate warm test confirmed the BIOS temperature response. The report retains both results and the test boundaries. This local build is not a published GitHub release.

## Lutra 0.6.0-preview.1 physical load test — 2026-10-15

See the [QUADRO / RTX 4090 report](validation/2026-10-15-quadro-rtx4090.md) for the hardware inventory, current curve settings, measured plot and test boundaries. In 535 samples over approximately nine minutes, pump/fan duty followed the configured curves and actual speeds returned to 801–803 rpm / 0 rpm. Peak coolant/GPU temperatures were 42.11 °C / 64 °C; saved application and controller settings were unchanged.

The exact-command cooldown criterion (20% pump and 0% fans) was **not met**: GPU remained at 41 °C and coolant at about 39.5 °C. The report preserves the harness's stricter `Passed: false` result and distinguishes it from the observed return to quiet physical RPM. This run did not repeat fault/fallback commissioning or actuate motherboard outputs.

## Earlier development validation

Development validation, 2026-10-14:

- Release builds for Core, Hardware, Service and WPF App.
- 23 core/protocol/release regression groups, including 2,000 generated quantized curves and hostile update packages.
- 9 WPF interaction regressions: live input identity, pending endpoint/point edits, accessible point selection, automation peer, live alarm labels, fresh/unavailable monitoring and honest RPM mapping.
- PowerShell updater health checks reject 13 unsafe activation states. Eight isolated transactions cover success, activation rollback, both rename failures, and process termination after stop, old rename, new activation and committed health; recovery lock exclusion is verified.
- Synthetic screenshot review at 1440px, 1280px and 1120px, English and Finnish.
- Read-only physical probe found QUADRO, AMD integrated GPU and NVIDIA RTX 4090. NVIDIA temperature readings and QUADRO readings were available without errors.
- Lutra 0.4 was commissioned on one QUADRO: GPU-fed D5 pump and water-temperature radiator fan control, increased cooling response, water safety fallback, stopped-service fallback and Windows service recovery verified. Saved hardware settings were restored and compared byte-for-byte. No motherboard output was actuated.

The public build is a preview, not a completed hardware compatibility certification. Remaining physical commissioning: additional QUADRO channels and software slots, more than one QUADRO, each motherboard/header combination, sleep/wake and reconnect behavior, service install/uninstall on a clean machine, extended sensor-loss testing. Test failures must not be hidden by changing fallback duty or disabling Windows protection.

To inspect the UI without hardware writes:

```powershell
Lutra.exe --demo
Lutra.exe --demo --page curves --language fi --width 1120 --height 900 --capture C:\Temp\lutra.png
```

Application UI screenshots in this repository use synthetic data. The physical-validation report above additionally contains a processed measurement plot with no device identifiers. Private raw telemetry and configurations are not published. Source output and packaged output must both be checked; producing a new package does not replace an installed controller.

