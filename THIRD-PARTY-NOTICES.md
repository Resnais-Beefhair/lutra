# Third-party components

Lutra's own code and geometric logo are MIT licensed. No Aquasuite binaries, decompiled source, UI assets, private USB captures or device serials are distributed.

## LibreHardwareMonitorLib 0.9.6

Unmodified library, MPL-2.0, with additional component notices in its source tree. Source for this exact release: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6 . Preserve the included notices under licenses when redistributing the package. Lutra uses the public IControl API for supported motherboard outputs. Kernel access is through upstream PawnIO integration; no WinRing0 driver is bundled by Lutra.

PawnIO itself is installed separately by the user from https://pawnio.eu/ . Source and license: https://github.com/namazso/PawnIO . Modules embedded by LibreHardwareMonitor are part of that upstream distribution; their source and notices are linked by that project.

## NuGet and .NET

Exact direct and transitive dependency versions and integrity hashes are recorded in packages.lock.json. The packages' original license metadata is reproduced in licenses/nuget-notices.json; copied license files are stored alongside it. The self-contained Windows build also includes Microsoft .NET runtime files and their original license/notice documents.

Source and package metadata:
- https://www.nuget.org/packages/LibreHardwareMonitorLib/0.9.6
- https://github.com/dotnet/runtime
- https://github.com/dotnet/wpf

## Art provenance

assets/lutra.svg is original vector geometry authored for Lutra. tools/Generate-Icon.ps1 rasterizes the same twin-channel letter L into assets/lutra.png and lutra.ico. No generated or stock image is used. Documentation screenshots are renders of Lutra's explicitly labelled synthetic demo mode.

