# ARCH-01

>  Authority prevents impossible state writes; it cannot reliably distinguish a skilled human from aim assistance, macros, pathfinding, information extracted from already-replicated chunks, or every timing exploit.

ofc lmao. I just don't want speed hacks or timer or fly to exist.

>  server needs its own low-resolution gameplay-light value. The client's realistic lighting remains cosmetic.

def agree. light levels like in vanilla Minecraft should exist. Imo the only thing dif from vanilla is I would rather them not be calculated svside per block update. One piston spammed on a tall ceiling could lag the server or redstone dust since it emmits light. Like this could be sth the server only calculates at an interval (convenient would be an interval similar to how often it would test if it should spawn a mob or sth).

I'd go with D but I'm assuming it would be kinda hybrid: i.e the simulation on the client and server would run at the same tickrate and in theory the same inputs on your client and the movement state changes relayed to the server should result in the same position in the end but obv we know packet loss is a thing and stuff so buffering packets in a way where timer hacks aren't possible is tricky. 

My main inspiration was actually Source networking. So glad it was mentioned here.

# ARCH-02

I agree block + block entities + w/e is the one Minecraft uses. Now there are situations where regular blocks should be handled a bit differently (such as when they are pushed and pulled by sticky pistons idk).

It will be tricky, I just don't want this to be one of those situations where having a bunch of chests in one place will lag out your game because any of them could open. Like the system has to be more intelligent than that.

# ARCH-03

Small disclaimer. I don't know jack shit about Godot. I just chose it because it was fully open source and stuff and licensing wasn't cancer.

# ARCH-04

> Local and remote gameplay must not drift into two code paths.

I think that will happen one way or another anyway.

> Target platforms are an owner decision. The first topology spike should cover Windows x64 and Linux x64; macOS, mobile, web, and consoles are not shipping commitments until helper-process and native-library packaging is demonstrated for each.

totally agree. linux and windows first (oh ye CI/CD will be interesting...)

I agree with going with C. But tbh I think we're overengineering. Like singleplayer rly only differs in the sense that you can pause the game.

# ARCH-05

> no supported mixins, reflection patches, raw engine collections, direct database access, or asynchronous mutation of live world state.

Oh speaking of this. So one thing I liked about sourcemod is that you could set one SQL server to connect to and make individual databases for each plugin. I think this would be great because Spigot plugins have this stupid tendency to use SQLite out of the box and lot of server owners don't know / care enough to use the same DB engine for everything. So if databases were kinda using some wrapper for plugins that'd be awesome actually.

I'd probably not have two different plugin ecosystems to maintain. The client and server are open source regardless. If someone wants sth that the plugin API cannot achieve they should 
 - Write an issue so we can expand the plugin API
 - Fork the project and add whatever modifications they want

Ideally the plugin API would be so great after enough iterations that most mods will be through it.

Sandboxed Lua sounds more similar with what we want, albeit WASM can mean we are able to write the plugins in a bunch of diff languages (albeit someone and that ain't me will have to write bindings for said languages). Like it sucks we can't do C# all the way (for the record, I don't like C# but I also hate having 100 languages in a single project). I guess Rust compiled to WASM? I really dk.

# ASSET-01

> Minecraft packs are input to an offline converter; `pack.mcmeta`, Minecraft directory names, block-model inheritance, and edition-specific behavior are never accepted as native runtime semantics.

for the record that can be in the shape of a script that is being given as-is with no expectation that it would give you a perfect or even usable result.

I agree with ZIP. Easy to send if we ever add server resource packs in the future and whatnot. 

This should be as engine-agnostic as possible. If someone wants to write a client in idfk Unity or UE or some other engine they shouldn't need to do some Godot conversion to be able to load in assets.

# ASSET-02

I have no idea wtf ur smoking. What dependency DAGs? If you're talking about resource packs stacked on top of each other...idk. I would probably not want textures to stack, probably a priority (i.e vanilla + one resource pack with a cool sword texture and a cool furnace model + one resource pack with a cooler sword texture and a cool model for spiders will be vanilla + a cool furnace model + a cooler sword texture + a cool model for spiders) in the order in which the packs are stacked

# ASSET-03

Ok maybe we can skip custom animations, but how do we make sure if mob models or chests or w/e have the same bones as vanilla so we can apply animations to custom ones? like I don't like the idea of glb and gltf.

# ASSET-04

> | Invent complete model/skeleton/keyframe format | Exact control | Large tooling/import/validation burden | Reject for v1 |

ok thing is...I wish we could do that. Ik this is ridiculous so I was just hoping sth would exist like a 3d model type specifically for voxel things that would also be far more efficient than minecraft's json implementaton and sth simple for animations.

# ASSET-05

We don't need custom GL shaders or sth for this. maybe sth like a node-graph for a material in UE would be cool tbh.
