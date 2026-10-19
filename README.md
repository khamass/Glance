# Glance

**A little eye contact. A little personality.**

![Glance icon](images/icon.png)

A Dalamud plugin for Final Fantasy XIV that activates the game's `/facecamera` gaze when you zoom in and stand still. Choose from 18 personality options, browse unlocked emotes, or keep gaze only. Optional Extended close-up adds tighter framing, fine zoom steps, and smooth camera-angle transitions. Gaze tracking itself does not rotate the body.

**Current public beta: [v0.5.2](https://github.com/khamass/Glance/releases/tag/v0.5.2)** (October 17, 2026), targeting Dalamud API 15. Camera tracking, expression timing, and camera-plugin compatibility are experimental. See [release notes](RELEASE-NOTES.md).

## Repository installation

Add this URL in `/xlsettings` → Experimental → Custom Plugin Repositories:

`https://raw.githubusercontent.com/khamass/Glance/main/repo.json`

Save, open `/xlplugins`, and install Glance. Update an existing repository installation through `/xlplugins`. Disable any development copy first. See [publishing instructions](PUBLISHING.md) for the release checklist.

## Getting started

1. Type `/glance` and enable **Meet my gaze**.
2. Stand still and zoom within **Close-up distance** (default 3 yalms). Gaze resumes after 0.4 seconds standing still.
3. Adjust **Close-up distance** from 1–10 yalms, or use **Use current zoom** to capture a valid distance in that range. **Gaze responsiveness** ranges from 5–90 (default 45); higher values track faster, within the game's native animation limits.
4. Choose a **Mood** under Personality. For face-focused close-ups, turn off **Allow body animations**; this is on by default.
5. Optionally enable **Extended close-up** and adjust the camera controls below.

Settings changes are saved automatically. `/glance on`, `/glance off`, and `/glance toggle` control the feature without opening settings.

## Personality

The mood menu has **18 options**. Themed pools use emotes available in your installed game and unlocked by your character; the examples below are candidates, not guaranteed available commands.

| Mood | Expressions or theme |
| --- | --- |
| Wink (classic, default) | Wink, followed by a smile |
| Playful | Wink, pucker up |
| Friendly | Smile, fake smile |
| Dramatic | Amazed, taunt |
| Expressive (joy / pose) | Joy, pose; includes body motions |
| Shy | Blush, shut eyes, look away, fidget, smile |
| Confident | Smirk, grin, pose, victory pose, thumbs up, flex |
| Cheerful | Beam, big grin, smile, joy, happy, chuckle, laugh |
| Mischievous | Wink, fake smile, smirk, taunt, sneer, cackle |
| Thoughtful | Think, ponder, hmm, doubt, question, contemplate |
| Affectionate | Pucker up, smile, blow kiss, dote, hug, heart |
| Melancholic | Sad, upset, worried, sigh, cry, sulk |
| Theatrical | Amazed, surprised, shock, stagger, gasp, panic |
| Celebratory | Joy, cheer, clap, huzzah, victory, victory pose, high five |
| Dance party | Available dances, including unlockable dances |
| Facial shuffle | Random known, unlocked facial expressions |
| Custom | Your selection from the searchable unlocked-emote catalog |
| Off - gaze only | Camera gaze without automatic emotes |

### Make it your own

Choose **Custom**, search by localized emote name or slash command, and tick the emotes you want. **Clear custom selection** empties the pool. Alternatively, choose an available preset and click **Copy this mood to Custom** to edit its eligible emotes.

The catalog comes from the installed game's data; it is not limited to the original eight expressions. New unlocks refresh within about a second, and switching characters clears the unlock cache. State controls such as `/sit`, `/groundsit`, `/cpose`, `/facecamera`, and `/straightface` are excluded. New selections use stable game row IDs; existing mood IDs, legacy Custom selections, and Joy/Pose timings remain compatible. Arbitrary user command strings are not executed.

**Allow body animations** filters every pool. Known facial commands count as facial; unfamiliar performance commands conservatively count as body motions. Empty or fully filtered pools leave gaze active without sending an emote. Random selection avoids consecutive repeats when more than one command is available.

### Frequency and duration

- **Every N zoom-ins / turns:** 1–10, default 4. Zoom-in and camera-turn counters are independent. A continuous drag counts once after at least 10 degrees of accumulated yaw/pitch travel and a 0.3-second pause; simultaneous triggers merge.
- **Wait after camera settles:** 0–5 seconds, default 2. Both zoom and rotation must settle before an expression starts; further camera movement restarts the wait.
- **Expression durations:** facial holds range from 0.1–5 seconds. New catalog body emotes default to an 8-second cooldown and support 0.1–30 seconds. Legacy Joy/Pose default to 4 seconds and retain their 0.1–5-second UI range.
- Legacy facial defaults are wink 0.3s, smile 2s, fake smile 1.8s, pucker up 1.4s, amazed 2s, and taunt 1.6s.

Wink transitions through a smile with one bounded smile retry; other expressions return directly to `/straightface`. Neutral cleanup also has one bounded retry after 0.65 seconds. Commands use `motion` to avoid emote chat messages. Dispatch does not confirm that the game accepted the animation.

Glance's expression sequences do not overlap. Zoom-out, pauses, and unload cancel pending actions and clean up the face; actor replacement discards old timers. Off sends no automatic emotes unless an existing Glance expression needs cleanup. Zoom out beyond the exit buffer to count another visit; movement while close does not create a new zoom-in. Reloading resets the counters.

**Body animations may move or turn your character, require a target or other conditions, or loop. Holds/cooldowns and `/straightface` control facial cleanup only; they do not guarantee the body animation stops. Move manually to stop a looping emote.** Native motions may outlast Glance's cooldown, and being unlocked does not guarantee usability in every context.

## Extended close-up and camera settings

Enable **Extended close-up** in `/glance` to scroll closer than the normal third-person minimum. It starts **off**. While eligible, only your own character bypasses proximity hiding at close range; other characters keep normal visibility rules. Camera target height and maximum zoom-out distance are unchanged.

| Control | Range | Default | What it does |
| --- | --- | --- | --- |
| Closest camera distance | 0.10–1.50 yalms | 0.50 | Closest permitted third-person distance; smaller values allow tighter framing |
| Fine close-up zoom | On/off | On | Enables finer zoom steps while Extended close-up is enabled |
| Close-up zoom delta | 0.01–1.00 | 0.10 | Smaller values give finer steps in both scroll directions |
| Close-up transition time | 0.10–1.50 seconds | 0.35 | Controls camera-angle easing response |
| Override close-up camera angle | On/off | On | Temporarily adjusts the game's configured camera angle |
| Close-up camera angle | 0–100 | 50 | Target 3rd Person Camera Angle |
| Angle activation distance | 0.20–3.00 yalms | 1.50 | Distance for the angle blend, independent of other plugins' camera limits |

**Close-up distance** still controls gaze activation and the fine-zoom threshold; **Closest camera distance** sets the final scroll limit. Angle activation has its own distance.

In v0.5.2, zoom step size blends smoothly from the original speed to your selected delta over the 0.75 yalms before **Close-up distance**, in both directions. Outside that blend band, normal zoom speed applies. Camera angle uses a similar 0.75-yalm distance band before **Angle activation distance**, plus frame-rate-independent time easing. **Close-up transition time** affects angle response, not the minimum distance or the width of the zoom blend.

### Restoration and troubleshooting

Glance captures the original **Character Configuration → 3rd Person Camera Angle** once and temporarily changes that actual setting through UiControl TiltOffset. The game settings can show the temporary angle during a close-up. Slider edits or external writes during that close-up do not replace the captured baseline.

Zooming out reverses the blend and restores the exact captured angle when easing completes. Disabling, activity pauses, camera-mode changes, camera replacement, and unload restore the angle immediately. The older live-only override and 0.05-yalm angle exit-margin descriptions no longer apply.

Disabling Extended close-up or Glance, pausing, or unloading releases camera overrides and restores the original minimum where the original camera remains available. A too-close third-person camera is moved back out to that minimum. Minimum restoration respects subsequent changes by other camera plugins.

**A crash or forced termination during a close-up may leave the temporary camera-angle setting in place.** If this happens, check and reset **3rd Person Camera Angle** in Character Configuration.

- Very small distances may put the camera inside the face. Increase **Closest camera distance** if geometry clips.
- First person remains separate and intentionally hides your character. Scroll back out before switching perspective.
- The settings show angle override active/waiting status. Repeated external angle changes may show a possible-conflict message; it cannot identify a plugin or guarantee compatibility. Compare with other plugins' angle controls disabled, or turn off Glance's angle override.
- Hook initialization failures leave normal camera limits in place and show a settings message. Other camera plugins may still affect framing or scrolling.

## Gaze behavior and compatibility

- Uses native `/facecamera` head and eye gaze. Gaze tracking itself does not rotate the body or write portrait camera-follow fields.
- Keeps plugin-owned gaze active while orbiting by experimentally identifying and smoothing the native camera vector. If the representation is unrecognized, it holds the original gaze and shows a diagnostic.
- Preserves gaze enabled manually before activation. If you cancel gaze manually while close, zoom out and back in to retry.
- A 0.35-yalm exit buffer prevents gaze flicker near **Close-up distance**. This is separate from camera-angle easing.
- Movement pauses gaze; it resumes after 0.4 seconds standing still. Gaze pauses during combat, PvP, mounts, casting, death, area transitions, cutscenes, occupied activity, group pose, first person, lock-on, and alternate cameras. Extended close-up also suspends during excluded activity/camera modes.
- Glance toggles off only gaze it enabled and that is still active. It attempts to restore plugin-owned gaze once if an expression cancels it.

This is an experimental custom-repository beta targeting **Dalamud API 15**. Compilation and automated checks do not establish live game compatibility. Native gaze detection, localized catalog data, unlock refresh, emote timing, close-up rendering, mouse/controller scrolling, and compatibility with other camera plugins require in-game verification. Group pose clones are not implemented. Game updates may require rebuilding against updated Dalamud/FFXIVClientStructs libraries.

Extended camera bounds are excluded by the [official Dalamud publishing restrictions](https://dalamud.dev/plugin-publishing/restrictions/); this feature is for the custom-repository beta.

## Install the development build

1. Build with `dotnet build Glance.csproj -c Release` (requires .NET 10 and Dalamud development libraries from XIVLauncher).
2. In FFXIV, open `/xlsettings`, go to Experimental, and add the full path to `bin\Release\net10.0-windows\Glance.dll` under Dev Plugin Locations.
3. Open `/xlplugins`, find Glance under Dev Tools, and enable it.
4. Type `/glance`. Stand still, then zoom within the default 3-yalm activation distance. Use **Use current zoom** to choose your own threshold.

`/glance on`, `/glance off`, and `/glance toggle` control the feature without opening settings.

If upgrading from the previous name, disable the old development plugin and replace its Dev Plugin Location with the new `Glance.dll` path. Glance imports the previous saved settings on first load when it has no settings file of its own.

## Build and verification

The project references the installed Dalamud libraries without copying them into the output. For a different location use `dotnet build -c Release -p:DalamudLibPath=C:\\path\\to\\dev`.

Run the standalone checks with `dotnet run --project tests/Glance.Checks.csproj -c Release`.

In game, verify head/eye tracking from front and sides, unchanged body orientation with body animations disabled, continuous orbiting and large rotations, walking/stopping, threshold behavior, first-person exclusion, logout/login, zone changes, combat suspension, and clean unload. Confirm normal targeting gaze resumes after disabling.

For Extended close-up, verify minimum-distance restoration, clipping, fine zoom in both directions, smooth threshold crossings, angle easing and exact restoration, and immediate cleanup on disable or excluded activity. Test with other camera controls disabled first, then compare compatibility. Check unlocked-emote filtering, localized search, Custom selections, facial cleanup, and manual stopping of looping body motions.

## Credits

Created by **Macki** ([khamass](https://github.com/khamass)). Source was developed with substantial AI assistance, with human design direction and in-game feedback. The icon was generated with AI assistance. Distributed under the [MIT License](LICENSE).

## References

- [Release notes](RELEASE-NOTES.md)
- [Dalamud plugin development](https://dalamud.dev/faq/getting-started/)
- [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs)
