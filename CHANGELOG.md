## v1.1.36
- Add DawnLib support!
- Fix some leftover Netcode bugs from V70 update
- Improve Lightning Particle Fix

## v1.1.34
- update to v70
- `default` in config will now use vanilla values instead of ItemClippingFix ones

## v1.1.33
- improve categorization system
- improve vertex caching

## v1.1.32
- use a better system to track modded items
- fix instantiation error Diffoz pointed out

## v1.1.31
- use a better cullingMask for computing vertexes

## v1.1.30
- only apply ViViKo rotations to Vanilla items

## v1.1.29
- Fix animated items reading the wrong vertical offset
- Fix alignment on most Placeable surfaces instead of limiting it to the Cupboard only

## v1.1.27
- fix rubber Ducky

## v1.1.26
- attempt to fix skinnedRenders again
- removed NameFixes ( they never worked )
- add fix for items generating inside furniture ( eg: lamps in kitchen counter )

## v1.1.25
- add VertexLibrary dependency
- fix some wrong rotations

## v1.1.24
- even more item rotations!
- hopefully fix skinnedRenders

## v1.1.23
- added some rotation values to the list

## v1.1.22
- remove CruiserFixes

## v1.1.21
- change a boolean to actually fix the SkinnedMeshRenders

## v1.1.20
- Improve offset detection from SkinnedMeshRenders

## v1.1.19
- Fix OOB for new Bag item

## v1.1.18
- more small tweaks
- reduced logging

## v1.1.17
- just a bunch of small tweaks
- some more exception handling

## v1.1.16
- make OutOfBound load patch actually work ( hopefully )

## v1.1.15
- fix items slowly clipping into the floor on lobby reload

## v1.1.14
- better handling of config values

## v1.1.13
- apply/store yRotation too

## v1.1.12
- Fix GraphicsBuffer leak
- add Compatibility config for GI and SIS

## v1.1.11
- Fix Apparatus spawning as plugged on lobby load
- only apply OOB and ItemFixes if we just joined ( localPlayer still null )

## v1.1.10
- apply OutOfBounds vertical offset on save instead of load

## v1.1.8
- tweaks for dropping items in the cruiser

## v1.1.7
- performance of ItemClipping vertex go Brrr.. ( thx DiFFoZ )

## v1.1.6
- only translate vertices once!

## v1.1.5
- Invert transform while calculating vertices

## v1.1.4
- Fix wrong placement of object inside CC

## v1.1.3
- Full rewrite of ItemClipping.VerticalOffset calculation

## v1.1.0
- rewrite OutOfBounds and Cupboard checks
- account for Cruiser in OOB check
- add ability to manually change resting rotation of any Item

## v1.0.21
- do not throw a fit in OutOfBounds if cupboard does not exist

## v1.0.18
- run namefix coroutine on StartOfRound to bypass More Company issues

## v1.0.17
- rewritten NameFix with help from @zaggy1024

## v1.0.16
- attempt at making NameFix work

## v1.0.13
- fix wrong object sizes caused by other mods adding Components to items

## v1.0.12
- softImplementation of AsyncLoggers API
- fix wrong bounds calculation caused by Unity AggressiveInlining

## v1.0.11
- attempt at reducing the impact of Readable Meshes on the visuals

## v1.0.10
- actually working BadgeFix

## v1.0.9
- fix minor bug with LobbyCompatibility SoftDependency
- added config toggle for BadgeFix

## v1.0.8
- Changes to NameFix

## v1.0.6
- Fixes to mesh stuff
- Fix Level overflow of players ( will work as host for everybody or as client for yourself )

## v1.0.5
- Fixed NameFix ( should not crash anymore )
- improved OutOfBounds and CupBoard
- added graceful handling of Mesh Exceptions
- added LobbyCompatibility as a soft Dependency

## v1.0.2
- improved NameFix ( should not crash anymore )
- OutOfBounds teleport only items below the ship
- improvements to ReadableMeshes

## v1.0.0
- improved handling of Cupboard and OutOfBound checks
- mark all meshes as readable
- add alternate lightning particle system

### Matty's fixes, version reset
## **FORK POINT**
- separated non-host features from [Lobby Control](https://github.com/mattymatty97/LTC_MattyFixes)

## v2.2.6
- fix crash with InvisibleManFix ( rpc handlers are static! )

## v2.2.5
- make the mod actually work (move harmony back to Awake)

## v2.2.4
- Fixes to InvisibleMan to avoid crashes
- Improvements to ItemClippingFix ( now all offsets are dynamically calculated based on the model affecting also modded items)
- Fixes to Late Joining ( prevent a 5th player from connecting while the 4th is still being processed)
- Added OutOfBounds patch to fix items glitching below the ship
- Improved CupBoard fixes to now track each shelf separately and snap items to them
- Added config entry to automatically re-open the lobby once in Orbit
- Actually remove the scrap limits ( previously was setting them to intMax )

## v2.2.3
- Use the correct LethalAPI dependency

## v2.2.2
- Added Config File

## v2.2.1
- Fully working lobby HotLoad
- Added Fixes for CupBoard ( storage closet ) items
- Improvements to invisible Player fix
- Removed item save limit, and scrap sync limits

## v2.2.0
- Added Experimental HotLoad of the lobby
- Added Fixes for various Radar Bugs
- Added Fix for invisible Players

## v2.1.1
- Full support of lobby status
- Addition to ste AutoSaving status and manual save command
- Integration of ItemClippingFix and expansion for host item rotation

## v2.0.0
- Fork Point and first rewrite with terminal commands

## OLD ShipLobby Changelog:

## v1.0.2

- Fixes the ship lever getting stuck if someone joins before the post-mission
  stats screen has finished displaying.
- Fixes the `Invite Friends` button working during a mission.

## v1.0.1

- Fixes an issue where the game would hang after attempting to leave the planet
  if BepInEx's `HideManagerGameObject` was not set to `true`.

## v1.0.0

Initial release.
