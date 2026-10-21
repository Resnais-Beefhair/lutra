# Architecture and failure behavior

Lutra.Core owns configuration, validation, curve interpolation and startup hysteresis, alarm timing, bounded history rendering and pipe serialization. Lutra.Hardware owns device discovery, QUADRO report encoding/transport and LibreHardwareMonitor adapters. Lutra.Service is the only normal writer. Lutra.App is a WPF monitor/editor that submits complete validated configurations.

## Ownership

QUADRO uses Windows' existing HID interface for status/settings and its WinUSB bulk interface for software temperatures. Control acquisition opens WinUSB exclusively and checks USB VID/PID, serial, interface and endpoint. Multiple devices are matched by identity. A second controller cannot acquire that same interface.

The service checks for Aquasuite or Vesi before applying or feeding controls. This cannot discover every third-party writer; do not run another fan-control application on the same outputs.

IPC is a bounded named pipe, denied to network logons, allowed only to LocalSystem, administrators and the installation owner. ProgramData is writable only by System/administrators; the owner can read it. The request protocol does not accept paths, executable names or arbitrary hardware commands.

Four independent pipe connections keep an idle client from blocking other clients. Reading a request has a three-second deadline; an accepted operation retains a separate 25-second deadline. Device changes still pass through the controller's serial queue, and payloads remain limited to 1 MiB. A missing/invalid owner SID or an unexpected listener failure is logged and included in `state.json` errors while cooling continues with its saved configuration. The transport retries after a delay without loosening the ACL. Restoring a valid owner file allows the listeners to start again.

## Configuration and writes

Rules are validated before writes: unique enabled output ownership, valid monotonic points, finite 0–100% limits, supported output and temperature input, nonzero held pump minimum, minimum pump fallback of 50%. That floor does not replace a manufacturer's device-specific requirement.

A QUADRO update starts from a fresh device settings report. Only selected channels and CRC change; unrelated channels, calibration and unknown bytes are preserved. The complete baseline is backed up locally by SHA-256. Settings are rechecked before writing, committed, then compared byte-for-byte on read-back. A failed write attempts rollback with a separate deadline. Failure and rollback errors are surfaced.

Updates across multiple devices cannot be electrically atomic. Completed devices are rolled back on a later write failure where possible. New controls that cannot be restored to an earlier rule are parked at fallback. After an error, inspect status and outputs rather than assuming a global transaction succeeded.

Deleting/disabling a configured QUADRO channel parks it at the previous fallback. Native water-temperature curves continue inside QUADRO when the PC application is closed. On service exit, motherboard channels that Lutra owns are returned to firmware with SetDefault.

## Missing data

Temperature values older than five seconds, non-finite values and missing values are invalid. Motherboard software rules use their configured fallback. QUADRO software sensor packets stop for the whole affected device if any required input fails or crosses its extra safety threshold.

Withholding the complete packet matters: observed QUADRO behavior does not immediately invalidate a previously valid software sensor merely because an individual slot is sent as missing. Continuing other slots could keep stale data alive. Its measured timeout is approximately 15 seconds after the last packet; this is a delay, not immediate emergency protection. Physical-temperature rules run locally.

A 20-second process watchdog and Windows service recovery handle a stalled host loop. They do not guarantee a motherboard's fan duty changes after an OS/kernel/driver crash. Set suitable BIOS defaults and validate hardware behavior. There is no automatic machine shutdown feature.

Configured GPU temperature inputs have a separate driver-recovery path. After 15 seconds without valid readings, the service recreates only the GPU sensor groups; QUADRO ownership and motherboard control remain active. It retries after another 30 seconds. If the input remains unavailable for a further 30 seconds, it durably records a restart budget, disposes normally and reports a service failure so Windows' configured recovery starts a fresh process. No stored temperature is substituted for missing data, no input is reassigned, and the ordinary fallback rules stay in effect.

The restart budget survives in `gpu-recovery.json`: one automatic process restart per uninterrupted outage and no more than one in 15 minutes, even across transient recoveries. Three consecutive valid samples clear the outage flag without clearing that cooldown. A missing GPU then receives GPU-only retries every five minutes. An unreadable budget or a failed budget write blocks the process restart. Console mode only retries GPU discovery. Genuine high temperatures do not trigger reconnection.

An installation owner can also request `refresh-gpu` over the existing protected pipe, or run `Lutra.Service.exe --refresh-gpu`. This recreates GPU sensor groups on the controller thread without changing configuration or applying a draft. The reply confirms that discovery was restarted, not that a missing sensor has already recovered.

## Data and notifications

The service records one frame every five seconds to UTC daily JSONL files, with 1–30 days of retention. Files can be read while appending. Queries keep first/last samples, minima/maxima and missing-data markers while downsampling. Gaps between service sessions are marked. A zero flow or RPM measurement remains a real zero.

History and status write failures do not intentionally stop control. Alarm state is evaluated by the service, logged on transition, and displayed/notified by the user's monitor process. Notifications are not delivered to a closed user session by the service.

## Installation and removal

The installer creates LutraControl under LocalSystem, automatic startup and recovery delays of 2/5/15 seconds. It does not install PawnIO, change Windows protection settings, or stop another cooling vendor's service. It refuses an active conflicting controller. Uninstall stops/removes the service and Start-menu shortcut but retains program files, configuration and history for recovery. QUADRO's persisted native curves and fallback behavior remain.

Device hotplug restarts discovery after a stale QUADRO connection. Hardware identifier changes require remapping rules; invalid destinations are rejected rather than assigned by display order.

