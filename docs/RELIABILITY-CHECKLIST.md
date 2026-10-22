# Reliability update validation

Validation checklist for v1.0.1. The maintainer reported completion of the in-game checklist on 2026-10-22. Automated validation and maintainer-reported gameplay validation are separate evidence.

## Implemented

- Settings edits preserve active gaze. Personality selection changes cancel the current expression and reset cadence without toggling gaze.
- Configuration saves wait until 500 ms after the latest edit. Immediate command/recovery saves consume any pending settings save; unload flushes remaining edits.
- Angle overrides save the original angle, character content ID and keyboard/gamepad mode before changing the game setting. Normal restoration clears the backup. An interrupted override requires a choice in Glance on the original character and control mode. Both recovery choices disable angle override until explicitly enabled again.
- Update failures save the disabled state and attempt camera restoration even if saving fails.
- Unload removes callbacks before hook disposal.
- Eligible expression pools and available-emote lists are cached until selection or unlocked emotes change.

## Automated validation

Run `dotnet build Glance.csproj -c Release` and `dotnet run --project tests/Glance.Checks.csproj -c Release`.

Checks cover debounce timing, backup validity and identity/control-mode matching, forgetting an override without writing, and the existing camera/expression checks. These do not exercise Dalamud UI callbacks, native hooks or game configuration persistence in a running client.

## In-game acceptance - maintainer-reported complete

1. Stand idle in close-up. Drag responsiveness, distance, zoom delta, angle and duration controls. Gaze should continue without repeated facecamera toggles; camera changes should remain smooth.
2. Change mood/custom emotes/body-motion eligibility during an expression. Gaze should remain active and the next expression must come from the new pool. Unlock another emote and confirm it appears within approximately one second.
3. Enter/leave close-up, disable the feature, and unload/reload normally. The original angle must return and no recovery prompt should remain.
4. With a disposable test configuration, retain a pending backup across a plugin restart to simulate interruption. Confirm the prompt appears; try Restore and Keep Current separately. Both must leave angle override disabled. Do not deliberately crash the game.
5. Log out during close-up, change characters, and change keyboard/gamepad mode. No saved angle may be written into a different character or mode. Resolve a retained backup using the original character and mode.
6. Close settings immediately after editing, then reload the plugin. Edits should survive. Confirm `/glance off` survives reload.
7. Check combat, mounts, cutscenes, group pose, first person and camera-plugin conflicts. Close-up hooks must pause as before.

## Deferred

Gaze-vector format detection is unchanged. Validate the native representation across camera yaw/pitch and character changes before replacing detection. CI/provenance, localization verification and larger state-machine refactoring are separate follow-ups.
