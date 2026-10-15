# Glance v0.2.2 — Public beta

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
