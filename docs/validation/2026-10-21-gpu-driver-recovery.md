# GPU driver update recovery

Date: 2026-10-21. Machine: NVIDIA GeForce RTX 4090, driver 621.01, Aqua Computer QUADRO, and the previously commissioned ASRock B650E PG-ITX WiFi CPU_FAN1. Library: LibreHardwareMonitorLib 0.9.7-pre767. Local fix build: 0.6.1-local.1; this is not a published release.

## Observed incident and immediate recovery

After the owner updated the graphics driver, the running service no longer exposed its selected GPU Core temperature. Some GPU memory temperatures remained available. NVIDIA's own tool reported 38.22 °C, and a separate fresh read-only Lutra process read the original selected GPU input at 39 °C.

The controller correctly stopped feeding the affected QUADRO's software temperatures. The pump reached its configured 79.98% fallback, approximately 3766 RPM; coolant was 37.66 °C and radiator fans stayed at 0 RPM. A normal service restart restored the GPU input and returned pump control to 20%, initially approximately 820 RPM. This supports a per-session/native-driver-state recovery problem; no exact native API failure code was captured.

The complete application configuration and the complete QUADRO settings report were backed up first. Both remained byte-for-byte identical through recovery and installation of the fix. This includes the pump, radiator and CPU curves, the existing alarms and flow calibration.

## Recovery behavior added

- A configured GPU temperature missing or invalid for 15 seconds triggers GPU-only rediscovery. It does not release motherboard control or QUADRO ownership.
- A second GPU-only attempt follows 30 seconds later. After another 30 seconds of failure, the service can request a clean process restart through Windows service recovery.
- The restart budget is saved before requesting that restart. Only one automatic process restart is allowed per uninterrupted outage and at most one in 15 minutes. Three consecutive valid readings clear the outage flag while retaining the cooldown.
- Persistent failure receives GPU-only attempts every five minutes. A budget read/write failure blocks the process restart. Console operation never automatically exits for this recovery.
- No missing value is replaced with a cached or fabricated temperature. The selected sensor is not reassigned to another GPU or to GPU memory. Genuine high temperatures retain the normal safety fallback and do not trigger rediscovery.
- The protected `refresh-gpu` IPC action and `Lutra.Service.exe --refresh-gpu` provide manual GPU-only rediscovery without applying a configuration draft.

## Verification

| Check | Result |
| --- | --- |
| Core/service regressions | 47 groups passed, including 10 GPU-recovery groups |
| UI regressions | 16 groups passed |
| Updater checks | Health, rollback, interrupted transaction and recovery-lock checks passed |
| Read-only recovery probe | Simulated loss of the selected input requested GPU-only refresh; the real GPU sensor returned at 39 °C in about 18.35 seconds |
| Installed manual rediscovery | Command succeeded; service process ID remained the same, with eight subsequent healthy samples |
| Final observed outputs | Pump 20%, approximately 802 RPM; radiator fans 0 RPM; CPU fan approximately 512 RPM; GPU 40 °C |
| Settings preservation | Complete configuration and raw QUADRO settings matched their pre-fix backups |

The recovery probe filtered the selected reading only inside a separate read-only process. It did not remove a Windows device, stop production sensor feeds or repeat a graphics-driver installation. The real production service's GPU-only rediscovery was then exercised separately through its installed command. Budget tests serialized the checkpoint and constructed a new policy instance to verify behavior across process boundaries.

The automatic clean-process escalation was tested as a deterministic policy decision, not by forcing a persistent live GPU outage or a production service failure. Windows service recovery actions and its non-crash-failure flag were inspected and were enabled. Future driver versions, adapter removal, kernel/driver crashes and combined sustained thermal load remain outside this test. The higher pump speed during genuine sensor loss is intended protection; recovery must not suppress it before a valid selected input returns.

Private telemetry, native device dumps, serial numbers, usernames and local configuration backups are not included in this report.
