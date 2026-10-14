# Compatibility

| Hardware / source | Monitoring | Control | Verification |
| --- | --- | --- | --- |
| Aqua Computer QUADRO, firmware 1033, settings structure 3 | Four physical temperatures, four RPM/duty channels, flow | All four output channels; native or software temperature curves | Protocol and hardware behavior characterized on one physical QUADRO; Lutra 0.4 reads verified. The new generalized writer is covered by synthetic report tests, not yet commissioned on hardware. |
| Other QUADRO firmware | Parsed when the known status layout is present | Read-only | Not claimed compatible for writes. |
| Multiple QUADROs | Independent serial-based identities | Independent ownership and slot assignment | Implemented; multi-device physical testing outstanding. |
| NVIDIA GPU | Temperatures exposed by LibreHardwareMonitor | Used as curve input | Current core/hotspot/memory readings verified on an RTX 4090. |
| AMD GPU | Temperatures exposed by LibreHardwareMonitor | Used as curve input | AMD integrated graphics discovery verified; temperature coverage depends on driver. |
| Intel GPU / CPU / motherboard temperatures | Library-supported sensors | Used as curve input | Availability depends on library, privileges and PawnIO. |
| Motherboard fan headers | Library-supported fan and control sensors | Actual IControl.SetSoftware / SetDefault integration for SuperIO channels exposing 0–100% | Implemented and software logic tested; no motherboard output was actuated during initial development. Physical mapping and response require per-board validation. |
| Other USB controllers, Aquaero, OCTO, D5 NEXT | Not currently supported by Lutra's controller driver | No | Do not infer support from similar vendor names or from library support elsewhere. |

Motherboard control is included in this preview. Enable it in Settings on an administrative service with the signed PawnIO driver installed. If no controllable SuperIO channels appear, Lutra reports that condition. Installing a driver cannot make an unsupported chip controllable.

A pump or fan connected to QUADRO is configured by **channel**, not detected by its brand/model. Mark pumps explicitly and choose a safe minimum. Effective RPM, startup duty and minimum duty differ by device.

Flow values are the controller's reported measurements. Lutra neither turns missing data into zero nor silently recalibrates sensors. A high flow 2 sensor requires the correct pulses/litre calibration for coolant and tubing. Low flow outside the manufacturer's measuring range can be intermittent even with steady pump RPM; smoothing is not a repair.

Windows 10/11 x64 is the target platform. No Linux, macOS, 32-bit or ARM64 support is claimed for this application.

