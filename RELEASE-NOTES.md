# Glance v0.3.0 — A little more personality

Choose Wink (classic), Playful, Friendly, Dramatic, Custom, or Off (gaze only) in `/glance`. Expression pools include wink, smile, fake smile, pucker up, amazed, and taunt. Random selection avoids consecutive repeats when possible.

Adjust frequency from every 1–10 zoom-ins or distinct camera turns, the settle delay from 0–5 seconds, and each selected expression's duration from 0.1–5 seconds. Existing users keep wink, every fourth trigger, and a two-second delay by default.

Wink retains its smile transition; other expressions return directly to `/straightface`. Sequences cannot overlap, clear on zoom-out, and discard old timers on actor replacement. Off avoids automatic facial commands. Build and checks cover selection, cadence, timing, overlap prevention, and cleanup. In-game verification is still required.

By **Macki**. A little eye contact. A little personality.

Glance brings your character's head and eyes toward the camera during idle close-ups, with occasional wink, smile, and neutral-expression sequences. Body orientation stays under your control.

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
