# Lutra

Lutra is an independent Windows desktop cooling controller and monitor for people who want to manage a liquid-cooled or air-cooled PC without Aquasuite. It is not affiliated with Aqua Computer.

## Platform and audience

Native Windows 10/11 x64, WPF and a separate Windows service. This is a Windows desktop product, not a web or mobile product. English is the default; Finnish is selectable. Enthusiasts should understand fan curves but should not need to understand USB protocols.

## Jobs

- Choose a temperature sensor and a supported fan or pump output for each independent curve.
- Set a start temperature, minimum, maximum and fallback output, and edit or straighten the curve.
- Monitor temperatures and flow with live dials, read actual RPM and PWM output, and compare simultaneous temperature and RPM trends. Overview offers five-, fifteen- and sixty-minute local-history ranges; the History page covers longer periods.
- Configure temperature alarms with delay and hysteresis.
- Use QUADRO and supported motherboard fan headers in the first public version.
- Find the project, releases and issue tracker from About & updates, and explicitly check for, download and install an available Windows update.

## Constraints

Hardware support must be reported accurately. Unsupported firmware or inaccessible motherboard controls remain read-only. Losing a sensor must not silently leave software control at a low output. Pump settings must preserve a nonzero minimum. A missing reading is never a zero measurement. History remains local.

Motherboard support uses LibreHardwareMonitor and its current PawnIO integration. Lutra does not disable Windows security features. Driver availability, board support and physical verification are separate facts.

The installed development baseline on 14 October 2026 is a healthy Lutra 0.4 installation; Vesi is disabled. The Lutra 0.5 redesign is developed against that running baseline. A development build, screenshot or package does not by itself change the installed version. Public source excludes private captures, identifiers and local diagnostics from the parent workspace.

## Confirmed design brief

The owner delegated naming and visual identity: Lutra, and explicitly corrected the visual direction on 14 October 2026 for use at a Windows desk in dim lighting. Dark graphite surfaces now extend throughout the application, with mint actions and restrained cyan, amber and violet measurement colors. The former light work area is superseded.

Build directly in the functioning application and evaluate screenshots. Overview restores prominent temperature and flow dials, large actual RPM and PWM readings, and simultaneous trends. Keep native Segoe UI, the original twin-channel L mark, clear device relationships, compact monitoring, visible state and reversible curve editing. About & updates makes the project links and update state explicit. Do not imitate Aquasuite visual assets or add RGB features.

## Release truth

Lutra remains an experimental public project. Document software support and test coverage separately from hardware combinations verified on physical devices. Keep the running version distinct from an available release; update checks happen only on request. No claims of universal motherboard support or certified cooling safety.
