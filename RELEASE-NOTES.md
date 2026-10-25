# Glance v1.0.2 — GPose handoff and validated gaze

By Macki.

- Add **Keep gaze and expression when entering GPose**, enabled by default. Glance discards scheduled expression cleanup before handing control to GPose, without sending `/facecamera` or `/straightface` on entry.
- Resume normal gaze handling after GPose exit, reclaiming only gaze Glance originally owned for the same character. Manually enabled gaze remains unowned.
- Replace gaze-format guessing with the world-position representation verified against native `/facecamera` captures. Incompatible or non-finite captures leave native gaze unchanged instead of guessing another format.
- Retain the existing v1.0.1 close-up camera implementation. Experimental camera-angle hooks and temporary test controls are excluded from this release.

Release build passes with zero warnings/errors and all 368 automated checks pass. The maintainer reports GPose entry, exit, zoom-out and gaze recovery working with the cleaned build. This does not establish compatibility with every game state or camera plugin. Existing settings are preserved. The existing persistent camera-angle override and recovery behavior remain unchanged.

Update through **Update plugins** in `/xlplugins`.

# Glance v1.0.1 — Reliability update

By Macki.

- Adjust settings without repeatedly restarting camera-facing gaze. Personality changes cancel the current expression and reset its cadence while keeping gaze active.
- Debounce configuration saves until 500 ms after the latest edit; flush pending changes on unload.
- Save the original camera angle before an override, associated with the active character and keyboard/gamepad mode. If an override is interrupted, Glance pauses angle overrides and offers **Restore saved angle** or **Keep current angle** on that character and control mode. Either choice leaves angle override disabled until explicitly enabled again.
- Unsubscribe callbacks before disposing hooks, ensure hook disposal is attempted if camera restoration fails, and persist the disabled state after an update error.
- Cache expression pools and available emotes until selection or unlock status changes.

Release build passes with zero warnings/errors; all 344 automated checks pass. The maintainer reports the in-game checklist completed. Automated checks do not exercise native hooks or prove compatibility with other camera plugins. Existing settings are preserved. Gaze-vector detection is unchanged pending controlled native-format validation.

Update through **Update plugins** in `/xlplugins`. A crash can still leave a temporary camera angle in the game configuration until recovered; the backup enables a recovery choice rather than automatic restoration during a crash.

# Glance v1.0.0 — First stable release

By Macki. Glance graduates from beta with the existing v0.5.2 feature set: camera-facing gaze, smooth extended close-ups, configurable camera angle and fine zoom delta, eighteen mood choices and a searchable catalog of unlocked emotes. Existing settings are preserved. This release updates versioning and release metadata without changing gameplay behavior.

Install or update through the custom repository. This is not an official Dalamud repository listing. Body animations may loop or require targets, and other camera plugins can interfere; facial cleanup does not stop every body motion. A game crash during a close-up may leave the temporary camera angle in place. These existing limitations remain applicable to the stable release.

Release build and automated checks pass. Stable status reflects the maintainer's release decision; it does not imply new in-game verification in this packaging run.

# Glance v0.5.2 — Smooth close-up transitions

Fine zoom now blends the original zoom speed into the selected delta across the 0.75 yalms before Close-up distance, with a smoothstep curve. The same band works in reverse when zooming out. There is no abrupt delta switch at the close-up boundary.

The camera angle uses a similar distance blend before Angle activation distance, plus frame-rate-independent time easing. **Close-up transition time** ranges from 0.10–1.50 seconds, default 0.35; it controls angle easing response. The original configured angle is captured once, remains the blend baseline, and is restored exactly when the exit blend completes. Disabling, pausing, changing camera mode or unloading still restores immediately rather than delaying cleanup. Minimum-distance limits remain unchanged.

Build and automated continuity, frame-rate and exact-restoration checks pass. Actual framing and scrolling with camera plugins still require in-game testing.

# Glance v0.5.1 — Fine close-up zoom

Adds **Fine close-up zoom** and a **Close-up zoom delta** slider under Extended close-up. Delta ranges from 0.01 to 1.00, default 0.10. Smaller values give finer zoom steps. The override applies in both directions while within the existing Close-up distance, so zooming back out is also controlled. Outside that distance the original camera zoom-speed function is used.

Fine zoom is enabled by default when Extended close-up is enabled. Turning it off, disabling Extended close-up/Glance, or entering an excluded camera/activity mode releases the zoom-speed hook. Camera replacement disposes the previous hook without dereferencing the old camera. The minimum camera distance and first-person restrictions remain independent. Other camera plugins may still interfere with zoom behavior.

Includes the unpublished v0.5.0 personality expansion below. Release build and automated delta bounds, activation and fallback checks pass; actual scrolling, mouse/controller input and camera-plugin compatibility need in-game verification.

# Glance v0.5.0 — Personality catalog

Adds eleven mood choices: Shy, Confident, Cheerful, Mischievous, Thoughtful, Affectionate, Melancholic, Theatrical, Celebratory, Dance party and Facial shuffle. Together with the existing seven options, the mood menu now has eighteen choices. Existing Wink, Playful, Friendly, Dramatic and Expressive pools retain their original definitions and saved numeric identities.

Custom now browses named performance emotes from the installed game's Emote sheet. Search by localized name or command. Only unlocked emotes are eligible; new unlocks refresh within a second, and switching characters clears the unlock cache. State controls such as /sit, /groundsit, /cpose, /facecamera and /straightface are excluded from personality performances. Themed pools resolve against real catalog commands and omit missing/locked entries. Catalog loading and in-game unlock filtering require runtime verification; compilation alone does not verify the game data.

Copy an available mood to Custom, select individual emotes and adjust their holds. Existing custom flag selections remain usable alongside new selections stored by stable game row IDs. No arbitrary user command strings are executed. Motion-only commands avoid emote chat text. Random selection avoids consecutive repeats by command, including catalog emotes that share the generic expression kind.

The Allow body animations toggle filters every pool. Known facial commands are classified as facial; unknown/new performance commands conservatively count as body animations. Facial shuffle uses only the known facial set. New catalog body emotes default to an eight-second cooldown and support 0.1–30 seconds. Facial holds support 0.1–5 seconds; legacy Joy/Pose retain their saved timing and five-second UI range. Wink retains its smile transition.

Body motions can move or turn the character, need targets or other conditions, or loop. Hold/cooldown and /straightface only control facial cleanup: they do not guarantee that a body animation stops. Move manually to stop a looping emote. Locked emotes are never automatically selected, but unlock status cannot guarantee that an emote is usable in every context. No consecutive emote sequences overlap in Glance's scheduler; native body animation lifetimes may exceed its cooldown.

Release build and automated checks pass for legacy compatibility, theme rules, generic-ID repeat avoidance, long cooldowns and cancellation. Actual emote execution, localized data, unlock refresh and body behavior still require in-game testing. Camera behavior is unchanged from v0.4.3.

# Glance v0.4.3 — Apply the game camera angle

Replaces the live TiltOffset field override with the actual UiControl TiltOffset configuration API. The prior implementation could be overwritten by the game's camera recalculation and its diagnostic could mislabel that as another plugin's interference.

Inside the configured close-up range, Glance temporarily sets Character Configuration's 3rd Person Camera Angle to its selected value. The original configured angle is captured once and restored on zoom-out, disable, activity pause, camera replacement and unload. Slider edits or external angle writes during the close-up do not replace that snapshot. The game configuration can temporarily display the selected value while close. Unlike v0.4.1/0.4.2, this does temporarily change the configuration setting itself. A game crash or forced termination during the close-up may leave that temporary setting in place.

The conflict diagnostic now observes the configuration setting, rather than a live camera interpolation field. Other plugins can still write later or change framing through other methods; disable their angle controls when testing. Build and automated configuration-capture/restore checks pass; in-game verification is required.

# Glance v0.4.2 — Camera angle controls

Under Extended close-up, choose **Override close-up camera angle**, set **Close-up camera angle** from 0–100 (default 50), and set **Angle activation distance** from 0.20–3.00 yalms (default 1.50). Activation is independent of camera limits changed by other plugins. The 0.05-yalm exit margin remains.

The UI shows whether the angle override is active. Repeated unexpected changes to the live angle produce a possible-conflict message; this is a heuristic and cannot identify a particular plugin. Disable another plugin's angle adjustment or Glance's angle override to compare. Other camera plugins may still affect framing or run after Glance. The saved game setting is unchanged and normal camera updates resume on exit.

# Glance v0.4.1 — Close-up camera framing

When Extended close-up is enabled and the camera moves below its original minimum distance, Glance temporarily applies **3rd Person Camera Angle 50**. Zooming out beyond that boundary restores the angle from Character Configuration. A 0.05-yalm exit margin prevents flickering at the boundary.

The override uses the live camera only: Glance never changes the saved Character Configuration angle. Pausing, disabling, unloading, switching camera modes or replacing the camera also releases the override. The native camera update reads the saved setting again on release, including changes made during a close-up.

Build and automated threshold checks pass; visual framing and restoration still require in-game testing.

# Glance v0.4.0 — Extended close-up

Enable **Extended close-up** in `/glance` to scroll closer than the normal game minimum. **Closest camera distance** sets the scroll limit from 0.10–1.50 yalms (default 0.50). The existing Close-up distance slider still controls when gaze activates.

During eligible third-person close-ups, Glance bypasses proximity hiding for your own character only. First-person mode remains available and intentionally hides the character; scroll out before switching perspective. Extremely small distances can put the camera inside the model: increase the slider if facial geometry clips. Camera target height is unchanged.

The feature starts disabled. Combat, PvP, mounts, casting, cutscenes, transitions, group pose and occupied activity suspend it. Disabling Glance, disabling the option or unloading restores the original minimum and moves a too-close third-person camera back to that minimum. Restoring respects subsequent changes from other camera plugins. Hook initialization failures leave normal camera limits in place and show a settings message.

Release build and automated ownership/restoration checks pass. Close-up rendering and scroll behavior still require in-game verification. This is a custom-repository beta; extended camera bounds do not meet the official Dalamud repository publishing restrictions.

## Previous: v0.3.1 — Joy or pose

New **Expressive** mood chooses between `/joy motion` and `/pose motion`, with no consecutive repeats. Both are available in Custom, with a default 4-second facial cleanup hold each. These are body-motion emotes: body animations finish naturally, including after zoom-out; `/straightface` resets the face only. Existing mood IDs and saved choices are preserved. Automated selection, timing, cleanup, and compatibility checks pass; in-game verification remains required.

Choose Wink (classic), Playful, Friendly, Dramatic, Expressive, Custom, or Off (gaze only) in `/glance`. Available choices include wink, smile, fake smile, pucker up, amazed, taunt, joy, and pose. Random selection avoids consecutive repeats when possible.

Adjust frequency from every 1–10 zoom-ins or distinct camera turns, the settle delay from 0–5 seconds, and each selected expression's duration from 0.1–5 seconds. Existing users keep wink, every fourth trigger, and a two-second delay by default.

Wink retains its smile transition; other expressions return directly to `/straightface`. Sequences cannot overlap, clear on zoom-out, and discard old timers on actor replacement. Off avoids automatic facial commands. Build and checks cover selection, cadence, timing, overlap prevention, and cleanup. In-game verification is still required.

By **Macki**. A little eye contact. A little personality.

Glance brings your character's head and eyes toward the camera during idle close-ups, with occasional expressions and optional body-motion emotes. Gaze tracking itself does not rotate the body.

- Configure with `/glance` or use `/glance on`, `/glance off`, and `/glance toggle`.
- Set your preferred close-up distance and gaze responsiveness.
- Every fourth zoom-in or distinct camera turn can queue a wink after the camera settles for two seconds.
- Pauses during movement, combat, cutscenes, and other game activity.

This is an experimental beta. Native camera-vector detection and facial-emote timing need broader in-game testing. A command being sent does not guarantee the game accepted it. Group pose is excluded. Tested builds target Dalamud API 15; future game updates may require a new build.

Source code was developed with substantial AI assistance, with design direction and in-game feedback from Macki. The icon was generated with AI assistance.

## Installation

After this release and repository file are published, add this URL under `/xlsettings` → Experimental → Custom Plugin Repositories:

`https://raw.githubusercontent.com/khamass/Glance/main/repo.json`

Save, open `/xlplugins`, and install Glance. Disable any development copy before installing the repository version.
