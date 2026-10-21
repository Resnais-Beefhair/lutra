# ASRock B650E PG-ITX WiFi CPU_FAN commissioning

Date: 2026-10-19. This report covers one motherboard header on one machine. It is not a general ASRock or Nuvoton compatibility certification.

## Configuration and dependency

| Component | Tested configuration |
| --- | --- |
| Motherboard / firmware | ASRock B650E PG-ITX WiFi / BIOS 3.08 |
| CPU | AMD Ryzen 7 7800X3D |
| Fan | Noctua NF-A12x25 PWM, as identified by the owner |
| Header / chip | CPU_FAN1 / Nuvoton NCT6686D, CPU Fan control and tachometer channel 0 |
| CPU input | AMD CPU Core (Tctl/Tdie), rather than the generic motherboard CPU temperature |
| Driver | Official signed PawnIO 2.2.0, installed separately by the owner |
| Lutra | Local commissioning build 0.6.1-cpufan.1; not a published GitHub release |
| Hardware library | Unmodified LibreHardwareMonitorLib 0.9.7-pre767, source commit `e68a7ef7326fa875210e89e1402d93a45ffe4d3e` |

LibreHardwareMonitor 0.9.6 has a firmware-mode restoration bug for this chip family. The [upstream correction](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/2527) is included in the pinned prerelease used here. The older installed library was replaced before handing over the header to Lutra.

The existing QUADRO GPU-temperature pump curve, coolant-temperature radiator curve and four existing alarms were preserved. The complete 961-byte QUADRO settings report was compared with its pre-test backup and remained identical. No USB device identifiers or private configuration files are included in this report.

## CPU fan settings

- Minimum 30%, held continuously below 55 °C; fan stopping is disabled by the nonzero minimum.
- Sixteen points on a straight line from a 0% curve response at 55 °C to 100% at 80 °C. After scaling to the 30–100% output range, effective duty is approximately 44% at 60 °C, 58% at 65 °C, 72% at 70 °C and 86% at 75 °C.
- Missing, stale or invalid input uses 100% fallback in the shared control logic. A separate CPU safety limit is 83 °C; the normal curve already requests 100% at 80 °C.
- Additional CPU warning: 80 °C for 30 seconds, with 5 °C reset margin. Additional high-temperature alarm: 85 °C for 5 seconds, with 5 °C reset margin. These notify; they do not shut down the computer.

## Observed checks

A bounded direct control trial requested 60%, 100% and 30%, without stopping the fan. End-of-step tachometer readings were 960, 1447 and 513 RPM respectively. Returning control to firmware restored approximately 469 RPM. The control-mode register was identical before and after restoration, including the other channels' mode bits. These are observed tachometer values, not a claim that the fan reached its manufacturer's nominal maximum speed.

The production service's high-temperature fallback path was exercised by temporarily setting only the new CPU rule's safety threshold below the measured CPU temperature. It requested 100% and reached approximately 1440 RPM. The normal CPU configuration was then restored and compared with the intended configuration. This tested the production safety-temperature path; physical sensor disconnection was not tested.

A separate CPU load test used two worker threads for 20 seconds, four for 35 seconds, and 75 seconds of recovery, preceded by an eight-second baseline. Heating would stop at 80 °C, with telemetry validity and fan rotation checked throughout. That stop threshold was not reached.

| Measurement | Baseline | Peak | Recovery, final ten samples |
| --- | ---: | ---: | ---: |
| CPU Tctl/Tdie | 49.50 °C | 74.38 °C | 45.33 °C |
| CPU fan speed | 509.4 RPM | 1256 RPM | 508.5 RPM |
| Actual fan duty | 30% | 84% | 30% |

All 163 samples fell inside the curve comparison envelope: the minimum and maximum expected commands from the current and previous two sampled temperatures, with a three-percentage-point allowance for command quantization and acquisition delay. This is a bounded timing comparison, not proof of zero control lag. The fan accelerated under load, slowed after load and met the strict recovery criterion of 30% duty in all final ten samples. No thermal alarm or controller error occurred in that load run.

Normal production-service shutdown restored the original firmware mode register, including the other channel bits. The first cold handback check **failed its separate rotating-fan criterion**: firmware selected approximately 3.5% duty and the tachometer read 0 RPM. That result is retained; it is not counted as a passing rotation check.

A follow-up check kept the header in firmware mode and applied two CPU worker threads for 20 seconds, with an 80 °C test stop limit. Firmware raised fan duty to 81% and speed from 329 to 1221 RPM; peak CPU temperature was 74.13 °C. The mode register remained in its original automatic state. This confirmed temperature response under firmware control, while the idle test showed that the existing BIOS profile can allow the fan to stop. Lutra's new profile instead holds at least 30%. The service was restarted after each check.

## Boundaries

Only CPU_FAN1 was commissioned. The similarly named CPU_FAN2/WP connector on this board is associated with the preinstalled M.2 fan; no other motherboard output was actuated. Physical header naming, attached fan model and nominal maximum speed were not independently instrumented beyond the controller's labels and tachometer response.

The run was a short staged CPU test, not a sustained all-core thermal test or a combined CPU/GPU workload. Sleep/wake, device removal, a kernel/driver crash and fan blockage were not tested. Motherboard outputs lack QUADRO's autonomous coolant curve and software-input watchdog behavior; a process, operating-system or driver failure does not guarantee immediate firmware restoration. Keep an appropriate BIOS fan profile configured.

Software regression checks passed: 37 core/service groups, 16 UI groups, updater health checks and isolated update/recovery transactions. Private raw measurements, device dumps, user paths and configuration backups remain on the test machine.
