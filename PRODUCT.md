# Lutra

Lutra is an independent Windows desktop cooling controller and monitor for people who want to manage a liquid-cooled or air-cooled PC without Aquasuite. It is not affiliated with Aqua Computer.

## Platform and audience

Native Windows 10/11 x64, WPF and a separate Windows service. This is a Windows desktop product, not a web or mobile product. English is the default; Finnish is selectable. Enthusiasts should understand fan curves but should not need to understand USB protocols.

## Jobs

- Choose a temperature sensor and a supported fan or pump output for each independent curve.
- Set a start temperature, minimum, maximum and fallback output, and edit or straighten the curve.
- See current temperatures, rotational speeds, flow and output power, including longer measurement history.
- Configure temperature alarms with delay and hysteresis.
- Use QUADRO and supported motherboard fan headers in the first public version.

## Constraints

Hardware support must be reported accurately. Unsupported firmware or inaccessible motherboard controls remain read-only. Losing a sensor must not silently leave software control at a low output. Pump settings must preserve a nonzero minimum. A missing reading is never a zero measurement. History remains local.

Motherboard support uses LibreHardwareMonitor and its current PawnIO integration. Lutra does not disable Windows security features. Driver availability, board support and physical verification are separate facts.

The existing installed controller continues operating while this replacement is developed. Public source excludes private captures, identifiers and local diagnostics from the parent workspace.

## Confirmed design brief

The owner delegated naming and visual identity: Lutra. Build directly in the functioning application and evaluate screenshots. Replace the old oversized dark-blue cards with a precise, approachable desktop instrument: clear device relationships, readable charts, compact monitoring, visible state and reversible editing. Do not imitate Aquasuite visual assets or add RGB features.

## Release truth

Initially an experimental public project. Document software support and test coverage separately from hardware combinations verified on physical devices. No claims of universal motherboard support or certified cooling safety.
