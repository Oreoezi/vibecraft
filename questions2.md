## FOUNDATION-00

Ok no. So vertical slice is a must. Max height idk sth big, let's say like 10k blocks tall. 

Ok fine we can do a fixed 60TPS tickrate. This is good enough to have responsive PvP and bridging

## GAME-01

> Saves may outlive engine releases and may be opened while an optional mod is missing.

Again if the save is moded we can be safe and just not allow ppl to open the save without the mod. I would use an uint32 i wouldn't care, it's just we should probably leave some space unreserved specifically for modded items. We can probably get away with it, we can probably store NBT separately for specific block indices, albeit I wouldn't worry about that for now.

## GAME-02

Go with whatever u want, i dont understand shit.

## NET-01

Sure go with hybrid

## NET-02

Go with recommended

## NET-03

GNS is fine albeit depends where we draw the line because Valve designed them for Steam Datagram Relays and whatnot. Let's try to keep it simple and see what exists already. We want to make a game, not reinvent the wheel. One thing where I think we kinda derailed is the fact that this is just one game, in many aspect it sounds like we are getting into a lot of rabbit holes with no mature open source implementations.

## NET-04

Well...We can go with A but let's make the code very good (I mean ideally the entire project should be written well). Some later version can add something similar to subtick for block placing or attacking.

## NET-05

We need to discuss this more, the doc lacks detail.

## NET-06

60TPS world loop.

## NET-07

Hear me out: we need out of the box native proxy support and a re-route packet or sth: i.e players can connect to vibepixel.net and be redirected to an EU server if they are from the EU for better ping and stuff. This can come after V1 but networking logic should not make this horrible to implement.

## NET-08

The philosophy is aight, I would like to think more as to first: if I have a public server, one player cannot under any circumstance take it down by themselves. Like the efforts to bot should be serious. Maybe PoW for connecting as an optional feature? who knows. Def not a V1 thing. For now as long as one player trying to send a lot of packets gets kicked and lag machines aren't exactly easy to make thanks to threaded chunks anways...idk. We might be good for now.

## NET-09

I don't rly care. Recommended w/e

## RENDER-01

Recommended.

## RENDER-02

I might need to research more into this. I don't rly understand jack.

## RENDER-03

Keep it in V1. But ye like keep in mind we will have fog and it will be *far* terrain so like...no need to make this rly beautiful.

## RENDER-05

> Store two logical 0–15 light values per block, interpolate them across visible surfaces in the fragment shader

again...the clientside lighting can be nice and smooth but the logic for mob spawning (aka light levels) can be as simple as in minecraft.
