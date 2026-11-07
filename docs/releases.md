# Release builds

The current prerelease is `v0.1.0-alpha.3`, adding a capture-only sharing window on top of the alpha.2 mode-list fix. Use the .NET SDK **10.0.403** for this pinned single-file packaging graph. Release runtime packs and the SDK's ILLink bundling tasks are pinned to **10.0.14**. Update the SDK/runtime pins, reviewed license allowlist and separate `packages.release.lock.json` files together for future servicing releases. Normal development retains its own `packages.lock.json` files.

```powershell
./scripts/verify.ps1
./scripts/package-release.ps1 -Version 0.1.0-alpha.3
```

Packaging performs a locked self-contained win-x64 publish, preserves the GPL license, NAudio notice and runtime package license/third-party notices, checks dependency licenses, smoke-tests the actual bundled EXE, then creates the distribution ZIP and SHA-256 file under `artifacts/release`. The ZIP contains one EXE, documentation and notices. Native .NET components extract to a user-writable location on first launch; the application requires no administrator privileges. Output is not code-signed.

For a clean source checkout, the pinned runtime and ILLink packages are restored during packaging. The runtime packages declare MIT and bring their own notices; the packaging script checks this metadata and includes every license/notice supplied at the package root. Package signing verification remains enabled. No downloaded executable outside the .NET toolchain is used.

After review, commit the complete source, then push the version tag. `.github/workflows/release.yml` verifies the source, builds/tests the package on Windows, and publishes a GitHub prerelease using the workflow's repository-scoped token. The ZIP and checksum are attached; GitHub's tag source archives provide corresponding application source and build scripts. Tag publication must follow explicit release authorization. Never replace an existing public tag or silently overwrite a release asset.

The GPU smoke test verifies D3D11 hardware upload/readback and format recreation without opening a capture device. Hosted runners with no hardware GPU report a skip; they never substitute WARP and label it hardware. To require a local hardware pass, run the built EXE with `--smoke-test --require-gpu`.
