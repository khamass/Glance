# Publishing Glance

Repository: `https://github.com/khamass/Glance` (public, default branch `main`).

## Prepare locally

Run `./scripts/New-Release.ps1` in PowerShell with .NET 10 and Dalamud development libraries installed. The script builds the plugin, runs the checks, verifies manifest/DLL version agreement, generates `repo.json`, and creates `dist/Glance.zip` plus a SHA-256 checksum. It never uploads anything.

The ZIP allowlist is exactly `Glance.dll`, `Glance.json`, `images/icon.png`, and `LICENSE`. No game libraries, Dalamud libraries, user settings, or obsolete builds are included.

## Publish the beta

1. Create the public repository `khamass/Glance` with default branch `main`.
2. Commit source, tests, scripts, README, LICENSE, release notes, publishing instructions, manifest, generated `repo.json`, and `images/`. Exclude `bin/`, `obj/`, `.dotnet/`, and `dist/` using `.gitignore`.
3. Create a GitHub Release tagged with the project version (for example `v0.3.0`), targeting the committed source. Mark it as a pre-release and use `RELEASE-NOTES.md` for its description.
4. Attach `dist/Glance.zip` and `dist/Glance.zip.sha256` to that release. The asset must be named `Glance.zip` because the store entry uses that exact path.
5. Verify anonymously that the repository JSON, icon, and ZIP URLs are accessible. Do not announce the installer URL until all three work.
6. Test a clean installation through the custom repository with development copies disabled. Confirm the author, icon, version, configuration, gaze, expression cleanup, and unload behavior.

GitHub pre-release status labels this beta on GitHub; the custom repository itself distributes it normally, without an additional Dalamud testing toggle.

Repository URL to share after publication:

`https://raw.githubusercontent.com/khamass/Glance/main/repo.json`

## Future updates

Increment the project version and manifest assembly version together, regenerate the package and repository JSON, and publish the matching tag and release assets. The script derives versioned asset URLs automatically. Commit the updated `repo.json` only when the corresponding assets are ready.

## Before an official submission

This beta packaging does not constitute approval by Dalamud. Address the experimental native camera representation matching and expression acceptance behavior, migrate settings to the Dalamud Windowing API, adopt the supported SDK/packager with dependency locking, and perform the in-game acceptance checks in README. Follow the current official submission rules and AI disclosure policy. Official-submission descriptions and AI disclosures must be written by the human author.

References: [custom repositories](https://dalamud.dev/plugin-publishing/custom-repositories/), [official submissions](https://github.com/goatcorp/DalamudPluginsD17), [AI policy](https://dalamud.dev/plugin-publishing/ai-policy/).
