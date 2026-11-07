# Third-party notices and dependency review

CaptureCord source is GPL-3.0-or-later. Windows Media Foundation/WASAPI are system APIs, not distributed third-party code. The .NET Windows Desktop Runtime is an installation prerequisite, not bundled in the framework-dependent artifact.

| NuGet package | Version | Dependency | License | Purpose |
| --- | --- | --- | --- | --- |
| NAudio.Wasapi | 2.2.1 | Direct | MIT | Endpoint enumeration, WASAPI capture/playback and Windows DMO resampler wrapper |
| NAudio.Core | 2.2.1 | Transitive via NAudio.Wasapi | MIT | Wave providers, bounded audio buffer and volume/sample conversion |

The App and test executable explicitly reference NAudio.Wasapi as well to avoid an SDK/NuGet transitive resolution issue observed during development. This introduces no additional package.

Both package manifests declare MIT; the pinned upstream source license was reviewed. MIT permits inclusion under GPL terms while retaining its notice. The complete upstream notice is preserved in [third-party/NAudio-LICENSE.txt](third-party/NAudio-LICENSE.txt). Source: [NAudio v2.2.1 license](https://github.com/naudio/NAudio/blob/v2.2.1/license.txt). The license text carries its upstream 2020 copyright date; package metadata additionally identifies Mark Heath, 2023.

The lockfiles record the resolved direct/transitive graph and package hashes. `scripts/check-licenses.ps1` rejects unknown package/version pairs and validates license expressions in restored manifests. This is an intentionally narrow reviewed allowlist, not a general legal compatibility detector. Re-review dependency source, metadata, graph and notices on every update. Test tooling adds no NuGet dependency. GitHub-hosted CI actions execute in CI and are not shipped as application libraries.
