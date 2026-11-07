# Contributing

Contributions, forks, bug reports and redistributions are encouraged. Contributions are accepted under GPL-3.0-or-later; retain copyright and third-party notices. No contributor license assignment is required. Commercial redistribution is allowed under the license's conditions.

Build with the .NET 10 SDK on Windows x64 and run `./scripts/verify.ps1`. Keep hardware-dependent code separate from deterministic logic. Add tests for behavior and regressions, and report physical hardware checks separately. Do not submit simulated results as hardware validation.

Keep changes focused and document user-visible behavior, resource ownership and verification. Do not add telemetry, subscriptions, vendor-specific device assumptions, privileged drivers or content-protection bypasses. Review licenses for every direct and transitive dependency; update lockfiles, the reviewed allowlist and notices together. Preserve English code, comments, documentation and UI.

Before opening a PR, describe the problem, resulting behavior, tests run, remaining limitations and hardware/Windows versions used. Sanitize endpoint IDs, user paths and private information from reports. See the code of conduct for community expectations.
