# Compatibility

| Hardware / source | Monitoring | Control | Verification |
| --- | --- | --- | --- |
| Aqua Computer QUADRO, firmware 1033, settings structure 3 | Four physical temperatures, four RPM/duty channels, flow | All four output channels; native or software temperature curves | One controller commissioned with Lutra 0.4. A [Lutra 0.6 physical load test](validation/2026-10-15-quadro-rtx4090.md) confirmed GPU-fed D5 pump and native coolant-temperature fan response, plus return to quiet RPM, on channels 1–2. Exact final 20%/0% commands were not reached. Earlier fallback/recovery commissioning is separate; remaining channels/configurations need physical testing. |
| Other QUADRO firmware | Parsed when the known status layout is present | Read-only | Not claimed compatible for writes. |
| Multiple QUADROs | Independent serial-based identities | Independent ownership and slot assignment | Implemented; multi-device physical testing outstanding. |
| NVIDIA GPU | Temperatures exposed by LibreHardwareMonitor | Used as curve input | Core/hotspot/memory readings and GPU-core input to a QUADRO pump curve observed under [real RTX 4090 load](validation/2026-10-15-quadro-rtx4090.md). |
| AMD GPU | Temperatures exposed by LibreHardwareMonitor | Used as curve input | AMD integrated graphics discovery verified; temperature coverage depends on driver. |
| Intel GPU / CPU / motherboard temperatures | Library-supported sensors | Used as curve input | Availability depends on library, privileges and PawnIO. |
| Motherboard fan headers | Library-supported fan and control sensors | Actual IControl.SetSoftware / SetDefault integration for SuperIO channels exposing 0–100% | ASRock B650E PG-ITX WiFi CPU_FAN1 was commissioned with the fixed library in a [separate local test](validation/2026-10-19-asrock-cpu-fan.md). Other headers/boards require individual physical mapping and response checks. |
| Other USB controllers, Aquaero, OCTO, D5 NEXT | Not currently supported by Lutra's controller driver | No | Do not infer support from similar vendor names or from library support elsewhere. |

Motherboard control is included in this preview. Enable it in Settings on an administrative service with the signed PawnIO driver installed. If no controllable SuperIO channels appear, Lutra reports that condition. Installing a driver cannot make an unsupported chip controllable.

The library is pinned to LibreHardwareMonitor 0.9.7-pre767 because 0.9.6 contains a firmware-mode restoration bug affecting NCT6683D/NCT6686D/NCT6687D. The upstream [restoration fix](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/2527) is required before commissioning those chips. This dependency is a prerelease; a supported sensor name alone does not establish the physical header mapping or safe fan response.

A pump or fan connected to QUADRO is configured by **channel**, not detected by its brand/model. Mark pumps explicitly and choose a safe minimum. Effective RPM, startup duty and minimum duty differ by device.

Flow values are the controller's reported measurements. Lutra neither turns missing data into zero nor silently recalibrates sensors. A high flow 2 sensor requires the correct pulses/litre calibration for coolant and tubing. Low flow outside the manufacturer's measuring range can be intermittent even with steady pump RPM; smoothing is not a repair.

The [15 October 2026 hardware report](validation/2026-10-15-quadro-rtx4090.md) lists the exact pump, radiator, fan group, flow sensor, software version, curves, measured range and limitations. Its findings apply to that setup; they do not certify every pump or fan attached to QUADRO.

Windows 10/11 x64 is the target platform. No Linux, macOS, 32-bit or ARM64 support is claimed for this application.

