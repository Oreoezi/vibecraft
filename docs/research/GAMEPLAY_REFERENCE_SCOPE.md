# Gameplay reference scope: what “Minecraft 1.0-like” should mean

Status: Proposed product interpretation

## Recommendation

Use Minecraft Java 1.0 as inspiration for the eventual survival arc, not as a compatibility specification or the first implementation milestone. Define VibeCraft's own rules explicitly and choose the small redstone subset already named in the spec rather than promising every historical pre-1.5 quirk.

The phrase “Minecraft 1.0-like” currently combines three different things:

1. the simplicity and visual/content density people associate with early Minecraft;
2. the actual Java 1.0 survival arc, which already included hunger, enchanting, brewing, breeding, structures, the Nether, the End, and a boss;
3. a redstone cutoff before Java 1.5, which potentially includes several releases beyond 1.0.

Those are not one coherent version target. VibeCraft should document the desired loop and component set rather than inherit accidental behavior from a date.

## Historical anchor

The Adventure Update was split across Beta 1.8 and release 1.0.x. Beta 1.8 introduced/reworked hunger, food behavior, sprinting, terrain/biomes, and structures ([Minecraft Wiki Adventure Update guide](https://minecraft.wiki/w/Java_Edition_guides/Adventure_Update%3A_Part_I)). Java 1.0 completed the release-era arc with features including the End/Ender Dragon, brewing, enchanting, animal breeding, and Hardcore; exact details come from community-maintained historical documentation because Mojang does not provide a complete supported behavior specification for that release ([Java version history](https://minecraft.wiki/w/Java_Edition_version_history)).

Java 1.5 was explicitly the Redstone Update and added the comparator, hopper, dropper, daylight sensor, trapped chest, redstone block, activator rail, and weighted pressure plates ([Redstone Update history](https://minecraft.wiki/w/Redstone_Update)). Therefore “pre-1.5 redstone” should at minimum exclude those devices, but it does not define update ordering, quasi-connectivity, piston edge cases, or bugs.

## Proposed VibeCraft gameplay target

### Product identity

VibeCraft v1 is an original voxel survival sandbox whose initial verbs are familiar to early Minecraft players. It is not wire-, save-, asset-, recipe-, timing-, or bug-compatible with Minecraft. Names, art, balance, generation, AI, and exact mechanics should be independently designed.

### Eventual foundation feature set

The long-term foundation may include:

- one primary generated dimension with biomes, caves, weather, and a small structure set;
- mining, placing, drops, inventory, crafting, tools, armor, melee, and bows;
- health, food, regeneration, death, respawn, and day/night;
- passive, neutral, and hostile entity categories represented by a small initial roster;
- furnace-like processing;
- a Nether-like alternate dimension and later an End-like capstone dimension;
- a deliberately small circuit system: dust/wire, direct power sources, torches/inverters, repeaters/delays, buttons/levers/plates, doors/trapdoors, pistons, and lamps or another visible output.

This is a product backlog, not one release definition.

### Explicitly excluded from the first circuit system

- comparators and analog container output;
- hoppers and inventory transport;
- droppers and dispenser automation beyond a minimal actuator, if any;
- observers;
- quasi-connectivity and other implementation accidents unless intentionally designed;
- block-update suppression, duplication, zero-tick, or similar exploit compatibility;
- exact Java Edition update order where it is not part of an intentional rule.

The engine should preserve a general scheduled-update and signal-component model so later devices can be added, but should not implement modern Minecraft's quirks speculatively.

## Milestone ladder

### M0: Headless world truth

- registries and one small block palette;
- deterministic flat/simple generated sections;
- authoritative place/break;
- crash-safe save, reload, and migration fixture;
- no polished client required.

### M1: Multiplayer building slice

- local and remote server connection through the same protocol;
- predicted walking/jumping and authoritative collision;
- chunk streaming, meshing, one resource pack;
- place/break with drops and reconnect persistence;
- two players under an impairment test.

### M2: Survival loop

- inventory, hotbar, item stacks, crafting, one tool progression;
- health, damage, death, respawn;
- day/night and one hostile plus one passive creature;
- minimal sound/animation/material pipeline.

### M3: World identity

- authored biome set, caves, weather, and a limited structure vocabulary;
- food and regeneration balance;
- furnace-like processing and broader equipment;
- generation-version/seam behavior proven.

### M4: Circuits

- deterministic block-update contract;
- wire, power source, torch/inverter, repeater, door, piston, and output lamp;
- budgets, chunk-boundary behavior, save/load, and runaway-circuit handling;
- no promise of Minecraft bug compatibility.

### M5: Additional dimensions and extension surface

- one alternate dimension after portals/teleports, world storage, and cross-dimension entity transfer are stable;
- second/endgame dimension later;
- sandboxed content extensions after the registries, saves, network capabilities, and security model survive internal use.

## Behavioral-spec format

For every mechanic, write examples rather than “same as Minecraft”:

```text
Mechanic: food-driven regeneration
Authoritative state: health, food, saturation/exhaustion equivalent
Inputs/events: eating, movement, damage, time
Update schedule: explicit server phase/frequency
Rules: exact thresholds and rates
Network presentation: predicted UI versus confirmed values
Persistence: fields and migration defaults
Edge cases: death, full inventory, reconnect, dimension change
Intentional differences from references: listed explicitly
```

Circuit mechanics additionally need truth tables, event-order examples, chunk load/unload behavior, maximum work budgets, and deterministic test fixtures.

## Greenlight questions for the owner

These choices materially affect design but need not block foundational engine research:

- Is hunger part of the desired identity, or merely inherited from “1.0” wording?
- Is the End-like dimension required for the first public release or only the eventual foundation?
- Should redstone feel familiar while intentionally fixing quirks, or should selected quirks be preserved for contraption compatibility?
- Is world generation aiming for early Minecraft's sparse/surprising terrain, modern biome coherence, or an original style?
- Should resource-pack conversion support only visuals, or also model/block-state conventions?

Until answered, use these defaults: hunger exists but is tuned later; alternate/end dimensions are post-survival milestones; circuits preserve obvious player-facing behavior but not bugs; world generation is original; Minecraft pack compatibility is an offline visual conversion tool.
