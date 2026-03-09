# OutOfThePast

Quality of Life improvements

*"It was the bottom of the barrel, and I was scraping it."*

## Summary
- **Adjust Payphone Call Delay**: Increases side-job payphone call window from ~5 minutes to 30-45 minutes
- **Sit And Talk**: Enables dialogue with NPCs while seated
- **Pass Time Improvements**: Fixes camera jolt and alarm UI bugs when using Pass Time while sitting
- **Suppress Target Brackets**: Removes [Target] brackets from all action prompts for a cleaner UI
- **Echelon Zone Restrictions**: Prevents purchasing apartments and side jobs appearing in echelon zones without the echelon perk
- **Extended Decor Placement**: Decor items are held further from the camera, making it easier to place items on surfaces like desks
- **Place on Ashtrays**: Looking at an ashtray with a cigarette butt equipped shows "Place" instead of "Throw"
- **Fix Wok Rotation**: Wok is no longer held upside-down when picked up


## Patches

### AdjustPayphoneCallDelay
When accepting a Side Job that starts with answering a payphone, the game gives the player roughly 5 in-game minutes
to reach the payphone - barely enough time to sprint there using auto routing, with no room for organic navigation.
This patch increases the delay to a random value between 30 and 45 in-game minutes (configurable),
giving the player time to use their map, walk at a normal pace, and pass time with their watch if they arrive early.

**Config:**
- `PayphoneCallDelay.MinimumDelay`: default 30 (minutes)
- `PayphoneCallDelay.MaximumDelay`: default 45 (minutes)

---

### SitAndTalk
Side Job Clients are always seated at a bench or table.
Currently, the player must stand to initiate dialogue, since sitting is a "locked-in" interaction
that normally blocks other locked-in interactions like talking/handcuffing/lockpicking.
This patch allows the player to remain seated while talking to an NPC,
letting them covertly sit across from/next to the client when discussing a job.

---

### PassTimeImprovements
Fixes two bugs related to using Pass Time while sitting:

1. **Camera jolt**: Selecting Pass Time, Set Alarm, or Cancel while seated causes the camera to snap forward.
   This happens because the game runs a full sit-down transition that resets the camera orientation,
   even though the player is already seated. This patch suppresses that transition.

2. **Alarm persists on item switch**: If the player opens the Pass Time menu (which equips the watch)
   and then switches to a different item via hotkey, the alarm UI and sounds persist.
   This patch cancels the alarm and resets the action set when a non-watch item is selected.

---

### SuppressAllTargetBrackets
Removes the `[Target]` bracket text from all action prompts (e.g., `Talk To [Person Name]` becomes `Talk To`).

In vanilla, equipping any item causes all action prompts to display a target in brackets,
even for actions unrelated to the equipped item. Since the target is almost always obvious
from the first-person camera and equipped item, the brackets add visual clutter without useful information.
Like all patches, can be disabled if undesired (or interferes with other mods).

---

### EchelonZoneRestrictions
Prevents players from accessing echelon zone content before unlocking the echelon perk:

1. **Apartment purchases**: Apartments in echelon zones cannot be purchased without the echelon perk.
   Without this, players could buy an echelon apartment and then have to trespass through
   the echelon hallway to reach their own home.

2. **Side job filtering**: Side jobs from clients who live in echelon zones are not posted to
   noticeboards until the player has the echelon perk. Murders and kidnappings are unaffected,
   preserving echelon zones as a challenging area for major cases.

Note: the game has an unused per-motive `disallowEchelonHome` field that was partially wired into
side job generation (but never enabled). This patch supersedes that dead code.

---

### ExtendedDecorPlacement
Increases how far decor items are held from the camera during apartment placement, making it possible to place items at the back of desks and tables without using freecam.

**Config:**
- `Decor.DecorPlaceDistance`: default 0.9 (distance from camera)

---

### PlaceOnAshtrays
Looking at an ashtray with a cigarette butt equipped shows "Place" instead of "Throw", letting you place the butt in the ashtray rather than throwing it.

---

### FixWokRotation
The wok item is held upside-down when picked up due to an incorrect rotation offset in its PhysicsProfile. This patch flips the X rotation by 180 degrees to correct it.

---

