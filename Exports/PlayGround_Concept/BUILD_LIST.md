# PlayGround remake: build list from concept_v1.png (2026-11-16)

## Layout (rings around the cauldron, about 50 x 50 m)
| Ring | Distance from the cauldron | What's there | Why (the defend-the-cauldron fight) |
|---|---|---|---|
| Centre | 0 - 8 m | Cauldron + fire ring, stepping stones, slime puddles. Keep it OPEN. | Inside ~7 m monsters go for the player, outside for the cauldron: the player needs room to fight here. |
| Playground | 8 - 18 m | Swing set, slide, dome, seesaws, merry-go-round, sandbox, giant toys, small breakables | Cover, things to break and throw, landmarks to know where you are, hiding spots for the hat. |
| Edge | 18 - 25 m | Jelly trees, lantern trees, slime boulders, crystal rocks, gravestones, statues, fences | Frames the arena. Big multi-hit breakables. |
| Gates | corners | 4 monster gates (stone/wood arch + purple mist) | Where monsters come in, so the player can see the waves coming. |
| Outside | 25 m + | Rocky hills with slime drips, dead trees | Closes the arena so there are no hard walls (the walls drop today). |

## Props
Reuse = something 429R already has (maybe with a repaint). New = model it.

| Prop | Role in the fight | Reuse / new | Priority |
|---|---|---|---|
| Cauldron + stone fire ring | The thing to defend | Reuse (Fire_Woods_n_Cauldron) | done |
| Jelly-bubble trees (pink/aqua/lilac/teal, striped trunk) | Edge, ghost slimes come out of them | Reuse Big_Tree_01 (chunkier low-poly crowns would match the concept better) | 2 |
| Lantern tree (hanging jack-o'-lanterns) | Light + the lanterns break off when hit, drop candy | New | 2 |
| Dead twisted trees | Outside ring | Reuse | done |
| Swing set | Landmark, the swing can be punched to swing | New | 1 |
| Slide | Landmark, cover; hat can hide under it | New | 1 |
| Candy-cane monkey-bars dome | Landmark; bugs climb it? | New | 2 |
| Bone seesaw | Throw something on one end, the other launches (fun physics) | New | 2 |
| Merry-go-round with pumpkin seats | Punch to spin; pumpkins on it break | New | 2 |
| Candy sandbox | Hat / candy hiding spot | New | 1 |
| Skeleton rocking horse | Big breakable (multi-hit), rocks when hit | New | 1 |
| Jack-in-the-box | Breakable: pops a slime or candy when hit (surprise) | New | 1 |
| Tin robots | Breakable; later could be a wind-up enemy | New | 3 |
| Toy blocks with faces | Breakable/stackable | Reuse Jenga blocks? or new cubes | 2 |
| Toy soldiers | Small breakables, throwable | New | 3 |
| Beach ball | Kick/punch/throw toy | New (or a repainted bowling ball) | 3 |
| Stone knight statue with pumpkin | Landmark; the pumpkin breaks off | New | 2 |
| Angel statue with candy wings | Landmark, breaks into pieces | New | 3 |
| Gargoyle cat | Small statue, throwable chunk | New | 3 |
| Jack-o'-lantern totem pole | Landmark; each head breaks off | New | 2 |
| Giant skull, glowing eyes | Landmark, ghost slimes | Reuse (Giant_Skull) | done |
| Slime-dripping boulders | Cover | New | 1 |
| Candy-crystal geodes | Big breakable: a shower of candy | New | 1 |
| Gravestones | Breakables, cover | New | 2 |
| Stepping stones, slime puddles | Ground detail | New / decal | 3 |
| Barrels, crates, candy baskets, pumpkins, candles | Small breakables everywhere | Reuse (Wood_Barrel, Wood_Box, Bedroom_Candy_Basket, Pumpkin_On_Tree, candles) | done |
| Picket fence pieces | Breakable lines between areas | New | 2 |
| Monster gates (arch + purple mist) | Spawn points | Reuse purple gate torches + spawn circle / Wall_Portal look, new arch | 1 |
| Rocky slime hills | Arena edge | New (big, very low poly) | 2 |

## Things to fix so it LOOKS like the concept in the headset
- Fog: today the PlayGround sits in the house fog (6 - 22 m, nearly black), so the arena is dark. The concept is bright and open. Like the bowling and Daruma stages, the PlayGround should lift the fog while it runs, and show the Halloween sky and moonlight.
- Light: moonlight from the sky's moon (warm orange) plus glowing accents (lanterns, cauldron, crystals, jack-o'-lantern eyes).
- Quest budget: chunky shapes, under ~2,000 triangles a prop, one shared texture atlas (flat colours + soft gradients, like the slime orchard repaint), no thin leaves or wires.
