# Preview validation

Development validation, 2026-10-14:

- Release builds for Core, Hardware, Service and WPF App.
- 19 core/protocol regression groups, including 2,000 generated quantized curves.
- 6 WPF interaction regressions: live input identity, pending endpoint/point edits, accessible point selection, automation peer and live alarm labels.
- Synthetic screenshot review at 1440px, 1280px and 1120px, English and Finnish.
- Read-only physical probe found QUADRO, AMD integrated GPU and NVIDIA RTX 4090. NVIDIA temperature readings and QUADRO readings were available without errors.
- The installed previous controller remained running. Lutra was not commissioned as the active writer and no motherboard output was actuated.

The public build is a preview, not a completed hardware compatibility certification. Remaining physical commissioning: generalized QUADRO writes and additional software slots, more than one QUADRO, each motherboard/header combination, sleep/wake and reconnect behavior, service install/uninstall on a clean machine, extended sensor-loss testing. Test failures must not be hidden by changing fallback duty or disabling Windows protection.

To inspect the UI without hardware writes:

```powershell
Lutra.exe --demo
Lutra.exe --demo --page curves --language fi --width 1120 --height 900 --capture C:\Temp\lutra.png
```

Screenshots in this repository use synthetic data and do not contain a user's serial numbers or telemetry. Source output and packaged output must both be checked; producing a new package does not replace an installed controller.

