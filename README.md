# OutOfThePast

Quality of Life improvements

*"It was the bottom of the barrel, and I was scraping it."*

## Summary
- **Adjust Payphone Call Delay**: Increases side-job payphone call window from ~5 minutes to 30-45 minutes
- **Sit And Talk**: Enables dialogue with NPCs while seated



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

