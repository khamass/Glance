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
