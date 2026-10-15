# Contributing

Use the .NET 10 SDK on Windows. Run tools/Build.ps1 before proposing a change. Keep control logic in Lutra.Core independent of Windows UI and transport code. Preserve English/Finnish labels together and follow DESIGN.md.

Hardware support needs documented identity, firmware, read-only captures with identifiers removed, a report-layout explanation, fallback behavior and explicit physical validation. Do not relax firmware guards merely because a report has the expected length. Add tests for failures, stale inputs, configuration ownership and rollback boundaries, not only a successful command.

Never commit credentials, local configurations, serial numbers, USB traces, telemetry or decompiled proprietary code. Hardware logs can include unrelated USB traffic. Use synthetic fixtures for public tests.

Opening an issue does not require publishing your full system report. Include app/library version, controller model/firmware, the exact symptom and a minimal redacted configuration.

Bug reports and hardware requests have dedicated issue forms. Use those forms for support; Discussions is not currently enabled. Security reports belong in [private vulnerability reporting](https://github.com/Resnais-Beefhair/lutra/security/advisories/new), not public issues.

## Dependencies and analysis

Dependabot checks NuGet and pinned GitHub Actions weekly. Commit regenerated `packages.lock.json` files with dependency changes; CI restores in locked mode. Review hardware-library changes for protocol and driver implications before merging them. CodeQL analyzes the C# solution on pull requests, pushes to main and weekly; its Windows job builds the solution explicitly without running hardware probes.

## Releasing

`Directory.Build.props` is the single source for the application version and bundled runtime patch (`LutraRuntimeVersion`). Update the runtime patch when Microsoft publishes servicing updates: self-contained installations receive runtime fixes through a new Lutra package, not through the machine's shared runtime. A .NET 10 SDK is required; the current package pins .NET 10.0.13.

1. Update `Version` and, when appropriate, `LutraRuntimeVersion`. For target framework or dependency changes, run `dotnet restore Lutra.sln --force-evaluate` and commit the lock files.
2. Run `./tools/Build.ps1 -Package` on Windows. It builds, tests and produces `artifacts/Lutra-<version>-win-x64.zip`. Move an existing ZIP aside before rebuilding the same version.
3. Merge the reviewed changes after Windows build and CodeQL checks pass. A manual **Release** workflow run builds, attests and verifies a candidate without publishing it; its ZIP remains a workflow artifact.
4. Create and push a tag exactly matching `Version`, for example `v0.6.0-preview.1`. The Release workflow rejects mismatches, runs the same checks, builds the package in CI, attests it, verifies provenance, and then publishes a GitHub release. Hyphenated versions are prereleases and never marked as the latest stable release.
5. Download the release ZIP and verify its provenance with `gh attestation verify <zip> --repo Resnais-Beefhair/lutra --signer-workflow Resnais-Beefhair/lutra/.github/workflows/release.yml`. Check discovery through the application's update flow and validate installation on a commissioned test system separately.

The workflow never replaces existing release assets. If publication fails after upload, the release stays a draft: inspect the failed run before publishing it manually. Never move an already published tag. Build attestations record CI provenance; they do not provide Authenticode signing, physical hardware certification, or protection against a compromised repository maintainer.

