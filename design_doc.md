# Architecture
- Godot game written with C# bindings
- Separate server written in C#
- Singleplayer uses a server under the hood
- Will try to be similar to Minecraft 1.0 in terms of gameplay (aka none of the newer things like elitras, maces, etc)
    - Aim is to extend it to be a different game altogether but very similar to Minecraft, but keeping things simple at first is a good idea

## Netcode
- Secure (anim states for movement, make movement cheats almost impossible just through good netcode)
- Good lag comp handling 
    - players with high ping or packet loss being able to play decently, if the world is out of sync, etc
    - i.e someone breaks the block under you but you moved to a different block on your screen by then, you should not get teleported into a free-fall
- Protobufs
- UDP
- High tickrate (changeable between 32, 64 and 128 tick)

## Server
- Threaded chunk gen and chunk ticking (simulating falling items, handling entity AI, etc)
    - (semi) Atomic writes for world save (i.e if the server loses power the world should not become corrupt)
    - Square chunks (aka no max height)
- Safe from DDOS attacks, plugin support out of the box, ability to assert that client-side mods are synced between clients
    - Not responsible for distributing client-side mods, just asserting that they are installed on everyone's client before joining

## Client
- Higher quality texture (64x64 instead of 16x16)
- Optimized meshes for chunks
- Far chunk render support through LoD
- High quality fog
- Voxel yet realistic lighting 
    - Support for emissive and reflective materials
    - Support for refractive materials and transparent materials
    - Support for animated textures either through keyframes for procedurally-generated materials and/or PNG sequences
    - Lighting will defuse kinda-realistically but different light levels will be applied to each pixel of a block (so 1/64 of a block)
    - Lighting will be calculated fully client-side
- Assets won't be embedded in the base game
    - The game's original assets (textures, models, sounds) are just another resource pack that will be shipped separately from the source code
    - Resource packs will be semi-compatible with Minecraft resource packs (probably through some conversion tool)
    - Resource packs will natively support custom models for blocks and entities and even custom keyframe animations (for entities or chests, etc)
    - Support for procedurally-generated assets (ability to use Perlin noise, etc, to generate textures)
- Native client-side mod support with scoped permissions (so people can't install malicious mods from the internet)


# Gameplay

- All the worlds in minecraft (Overworld, Nether, End)
    - Biomes
    - Structures
    - Weather
    - Caves
- Crafting
    - Tools
    - Armor
    - Swords
    - Bows
- Smelting / Cooking
- Eating
- Regen
- Friendly mobs / Animals 
- Hostile mobs
- Neutral mobs
- Redstone
  - Pre 1.5 redstone at first (so just pistons, dust, redstone torches, repeaters, doors, etc)
  - Engine should be really efficient and flexible to support adding modern redstone later down the line
