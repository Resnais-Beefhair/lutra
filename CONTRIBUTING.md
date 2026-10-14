# Contributing

Use the .NET 8 SDK on Windows. Run tools/Build.ps1 before proposing a change. Keep control logic in Lutra.Core independent of Windows UI and transport code. Preserve English/Finnish labels together and follow DESIGN.md.

Hardware support needs documented identity, firmware, read-only captures with identifiers removed, a report-layout explanation, fallback behavior and explicit physical validation. Do not relax firmware guards merely because a report has the expected length. Add tests for failures, stale inputs, configuration ownership and rollback boundaries, not only a successful command.

Never commit credentials, local configurations, serial numbers, USB traces, telemetry or decompiled proprietary code. Hardware logs can include unrelated USB traffic. Use synthetic fixtures for public tests.

Opening an issue does not require publishing your full system report. Include app/library version, controller model/firmware, the exact symptom and a minimal redacted configuration.

