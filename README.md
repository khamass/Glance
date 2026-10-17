# Glance

**A little eye contact. A little personality.**

![Glance icon](images/icon.png)

A Dalamud plugin for Final Fantasy XIV that activates the game's `/facecamera` gaze when you zoom in and stand still. Add a little personality with facial expressions, or keep gaze only. The body keeps its existing orientation.

**Public beta:** camera tracking and expression timing are experimental. [Releases](https://github.com/khamass/Glance/releases).

## Repository installation

Add this URL in `/xlsettings` → Experimental → Custom Plugin Repositories:

`https://raw.githubusercontent.com/khamass/Glance/main/repo.json`

Save, open `/xlplugins`, and install Glance. Disable any development copy first. See [publishing instructions](PUBLISHING.md) for the release checklist.

## Credits

Created by **Macki** ([khamass](https://github.com/khamass)). Source was developed with substantial AI assistance, with human design direction and in-game feedback. The icon was generated with AI assistance. Distributed under the [MIT License](LICENSE).

## Install the development build

1. Build with `dotnet build Glance.csproj -c Release` (requires .NET 10 and Dalamud development libraries from XIVLauncher).
2. In FFXIV, open `/xlsettings`, go to Experimental, and add the full path to `bin\Release\net10.0-windows\Glance.dll` under Dev Plugin Locations.
3. Open `/xlplugins`, find Glance under Dev Tools, and enable it.
4. Type `/glance`. Stand still, then zoom within the default 3-yalm activation distance. Use **Use current zoom** to choose your own threshold.

`/glance on`, `/glance off`, and `/glance toggle` control the feature without opening settings.

If upgrading from the previous name, disable the old development plugin and replace its Dev Plugin Location with the new `Glance.dll` path. Glance imports the previous saved settings on first load when it has no settings file of its own.

## Behavior

### Personality settings (v0.3.0)

Open `/glance` and choose a mood:

| Mood | Expressions |
| --- | --- |
| Wink (classic, default) | Wink, followed by a smile |
| Playful | Wink, pucker up |
| Friendly | Smile, fake smile |
| Dramatic | Amazed, taunt |
| Custom | Any combination of the six expressions |
| Off | Camera gaze only; no automatic facial emotes |

Pools choose randomly and avoid consecutive repeats when more than one expression is selected. An empty custom pool behaves like Off. Wink remains the default for existing settings.

Set frequency from every 1 to 10 zoom-ins or distinct camera turns (default 4). The counters remain independent. Set the camera-settle wait from 0 to 5 seconds (default 2). Under **Expression durations**, adjust each selected expression's hold from 0.1 to 5 seconds. Defaults: wink 0.3s, smile 2s, fake smile 1.8s, pucker up 1.4s, amazed 2s, taunt 1.6s.

Wink retains its smile transition and one bounded smile retry. Other expressions return directly to `/straightface`. Neutral cleanup gets one bounded retry. Expression sequences never overlap, discard delayed steps when the actor changes, and clear on zoom-out, pause, or unload. Off does not issue a zoom-out emote unless an existing Glance expression still needs cleanup.

### Gaze and trigger behavior

- Zooming out beyond the close-up exit buffer sends `/straightface motion` and cancels pending wink/smile actions, even if movement already paused gaze. A single follow-up 0.65 seconds later covers an expression command rejected during the emote lock.

- Version 0.1.12 keeps the 0.3-second smile attempt and repeats it once 0.35 seconds later, because text-command dispatch does not confirm that the game accepted a facial-expression change. The straight-face timer restarts from that follow-up. This is a bounded fallback, not confirmation of the game's animation-lock timing; verify in game.

- Every fourth distinct camera turn can also queue the wink sequence while close and idle. A turn needs at least 10 degrees of accumulated yaw/pitch travel and a 0.3-second pause to finish. A continuous drag counts once. Zoom and turn counters are independent; simultaneous triggers merge into one pending wink. Both zooming and rotation must settle for 2 seconds before the wink, and expressions cannot overlap. Incomplete turns are discarded when gaze pauses.

- Every fourth eligible zoom-in waits until the camera zoom distance has stopped changing for 2 seconds, then sends `/wink motion`, followed 0.3 seconds later by `/smile motion`, then another 2 seconds later by `/straightface motion`. Further zooming restarts the wait. The `motion` suffix avoids emote chat messages. Pausing cancels a queued wink; pausing or unloading completes pending expression cleanup immediately for the same logged-in actor. Zoom out beyond the exit buffer to count another visit; moving or changing settings while close does not count again. The counter resets when the plugin reloads.
- Version 0.1.6 captures the native gaze vector immediately on activation to avoid missing its camera match during orbiting. After a wink or smile it checks once for cancelled plugin-owned gaze and restores it if needed, without toggling an active gaze off.

- Native `/facecamera` head and eye gaze. The game controls its animation and tracking.
- Version 0.1.10 increases target responsiveness another 50% (default 45); saved settings are upgraded automatically. Adjust **Gaze responsiveness** from 5 to 90 for slower or faster tracking. The native head animation may impose its own speed limit.
- Keeps plugin-owned gaze enabled during camera rotation. Attempts to identify the native camera-vector representation from the value captured by the game, then updates that vector with smoothing. If no representation matches, holds the original gaze and displays a diagnostic in settings. Manually enabled gaze is left alone.
- Resumes after 0.4 seconds standing still. Stops when movement is observed.
- A 0.35-yalm exit buffer prevents flickering at the zoom threshold.
- Pauses for combat, PvP, mounts, casting, death, area transitions, cutscenes, occupied event states, group pose, first person, lock-on, and alternate cameras.
- Sends `/facecamera` once on entering range and again when leaving, pausing, disabling, or unloading, only if Glance enabled the gaze and it is still active.
- Preserves gaze that was already enabled manually. If you cancel it manually while zoomed in, zoom out and back in to reactivate it.
- Does not write portrait camera-follow fields or body rotation.

## Build and verification

The project references the installed Dalamud libraries without copying them into the output. For a different location use `dotnet build -c Release -p:DalamudLibPath=C:\path\to\dev`.

Run the standalone math checks with `dotnet run --project tests/Glance.Checks.csproj -c Release`.

In-game acceptance checks: confirm head and eye tracking from the front and sides, unchanged body orientation, smooth orbit tracking, release on walking, reactivation after stopping, threshold behavior, first person exclusion, logout/login, zone changes, combat suspension, and clean plugin unload. Confirm normal targeting gaze resumes after disabling.

Version 0.1.3 removes the jitter-producing off/on refresh cycle. It uses `/facecamera` for activation and cancellation only, with experimental direct camera-vector updates while active. The settings show whether the native gaze flag is active or the vector representation is unrecognized. Compilation does not establish live game compatibility; verify in game, including continuous orbiting, large rotations, and movement. Group pose clones are not implemented. Game updates may require rebuilding against updated Dalamud/FFXIVClientStructs libraries.

## References

- [Dalamud plugin development](https://dalamud.dev/faq/getting-started/)
- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs)


