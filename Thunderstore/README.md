Matty's Fixes
============
[![GitHub Release](https://img.shields.io/github/v/release/mattymatty97/LTC_MattyFixes?display_name=release&logo=github&logoColor=white)](https://github.com/mattymatty97/LTC_MattyFixes/releases/latest)
[![GitHub Pre-Release](https://img.shields.io/github/v/release/mattymatty97/LTC_MattyFixes?include_prereleases&display_name=release&logo=github&logoColor=white&label=preview)](https://github.com/mattymatty97/LTC_MattyFixes/releases)  
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/mattymatty/Matty_Fixes?style=flat&logo=thunderstore&logoColor=white&label=thunderstore)](https://thunderstore.io/c/lethal-company/p/mattymatty/Matty_Fixes/)


A collection of Fixes for the vanilla game, with focus on vanilla compatibility.

some of the changes will only be visible to players with the mod installed ( it is suggested for everybody to have the mod )

**This mod is 100% Vanilla Compatible** and does not change any of the vanilla gameplay.

Patches:
--------
- ### Storage Cabinet
  - **fix items inside of Storage Cabinet falling to the ground on load**
  - **fix items on top of Storage Cabinet falling to the ground on load**
- ### ItemClippingFix
  - **fix rotation of some items while dropped**
  - **prevent items from clipping into the ground**
- ### RadarFixes
  - **fix orphaned radar icons from deleted scrap**  
  ( scarp sold will appear on the radar in all the maps )
  - **fix items from a newly created lobby being visible on the radar**
- ### OutOfBounds Patch
  - prevent items from falling **below of the ship**
- ### Alternate Lightning Particles
  - show particles around items in a sphere
- ### Readable Meshes
  - mark all meshes as readable to fix various vanilla bugs:
    - Stormy weather spamming zero_surface_area warnings
    - Lightning particle showing as a point instead of on the item model
    - align items to the floor instead of clipping/floating
- ### **Experimental** Name De-sync
  - prevent "Unknown" names for late joiners
  - correctly apply names to radar icons

Differences to [ItemClippingFix](https://thunderstore.io/c/lethal-company/p/ViViKo/ItemClippingFix/)
------------------------
This mod uses the same values from ItemClippingFix but additionally:
- fixes the rotation of Objects in a newly hosted game  
- fixes items clipping into the ground ( included modded ones )

Differences to [CupboardFix](https://thunderstore.io/c/lethal-company/p/Rocksnotch/CupboardFix/)
------------------------
CupboardFix removes the gravity from all item types that are above the ground and never resets it,  
this causes a lot of items to spawn floating both from the DropShip and inside the Factory.  
This mod instead only affects the items specifically inside the Closet and above it,  
additionally snaps the items to the shelves and forces the parent to the closet itself allowing you to move them together with the closet,  
as would happen if you had deposited the items manually inside 

Installation
------------

- Install [BepInEx](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/)
- Unzip this mod into your `Lethal Company/BepInEx/plugins` folder

Or use the mod manager to handle the installing for you.

