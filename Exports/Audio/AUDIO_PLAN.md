# Audio plan: the sounds the game is missing (2026-11-25)

Made from a full read-only check of the project: every sound slot in the scripts, prefabs, settings and
`2 Game Scene`, every PlayMaker sound action, and the notes. Nothing was changed.

What exists today: `Assets/Audio/SFX` 222 clips (voices, pumpkins, bugs, hits, magic, 39 music files) and
`Assets/Audio/Kenney Audio` 288 (UI clicks, arcade, sci-fi). There is **no ambience at all**, no footsteps,
and several moments are silent.

## How to make them on ElevenLabs

- **Sound effects:** ElevenLabs > Sound Effects. Paste the prompt, set the length, pick the best of the takes.
  Add this to the end of every prompt: *"cartoon game sound, playful-spooky, clean, no music, no voice"*.
- **Loops** (marked LOOP): turn on Loop if your plan offers it. Download **WAV** where you can: an MP3 adds a
  tiny silence at the start, which makes a gap in loops and a lag on short hits.
- **Voices:** your rule so far is "the voices are my own recordings". Three ways, your choice:
  1. record them yourself, as before;
  2. record yourself and run it through **Voice Changer** (it keeps your timing and acting, gives one steady
     character voice for the Fairy; design it as an adult voice, see P1_PROMPTS.md);
  3. type them into **Text to Speech** with a designed voice.
  Use the same voice for all of the Fairy's lines.
- **Names and folder:** save each file with the name in the tables below into
  `Assets/Audio/SFX/ElevenLabs/<group>/` (groups: Voice, Player, Enemy, Boss, Weapon, UI, Stage, Story, World,
  Ambience). Several takes = `_1`, `_2`, `_3`. Tell Claude "the sounds are in" and they get wired to their slots.
  (Last night's sounds went straight into `Assets/Audio/SFX/`; that works too.)

---

## TODAY 2026-11-26: the story sounds (19 files)

User: "I want to complete more with the story telling of the game." These are the story moments that are still silent
(or use a stand-in), in the order the player meets them. Same recipe as above. Only `fairy_eek` is a voice.

| File | Moment | ElevenLabs prompt | Length | Takes |
|---|---|---|---|---|
| `amb_dark_walk` | The first dark walk, until the floor shows up | low dark drone, cold wind, empty endless space, faint far echo | 20 s LOOP | 1 |
| `light_return_swell` | The dark walk ends: "the floor starts to show up" | warm rising magical swell, soft shimmer opening up | 2.5 s | 1 |
| `fairy_eek` (voice) | She looks up and sees the pumpkin falling on her | a tiny startled gasp "Eek!", small fairy girl | 0.5 s | 2 |
| `start_pumpkin_land` | The first pumpkin slams down on her spot | huge heavy pumpkin landing thud, ground shake, debris | 1 s | 1 |
| `start_pumpkin_crack` | The first pumpkin cracks at half health (silent now) | sharp gourd crack crunch, splitting rind | 0.5 s | 1 |
| `lamp_flicker` | A laughing pumpkin laughs: the room lamp stutters | electric lamp flicker buzz, unstable crackle | 1.5 s | 1 |
| `cold_gust` | ... and the room turns cold | cold breathy ghostly gust of wind | 1.5 s | 1 |
| `miniboss_pop_burst` | The mini boss bursts out of the broken laughing pumpkin | big wet pumpkin burst, squelch and pop | 1 s | 1 |
| `boss_dig_rumble` | It digs into the floor / climbs back out | dirt shifting rumble, scraping claws | 1.5 s | 1 |
| `seed_chime` | Each seed piece pops out over the dirt (rooftop seal) | bright magical chime, single sparkle | 0.5 s | 2 |
| `seed_whoosh` | The pieces fly to the dirt | sparkly magic whoosh trail | 1 s | 1 |
| `seed_merge` | They join into one seed | shimmering magical merge, rising chord | 1.5 s | 1 |
| `seed_plunge` | The seed dives into the dirt | soft earthy plunge with a magic thump | 1 s | 1 |
| `tree_grow` | The big tree grows and lights the night | magical bloom with wood creaking and growing, warm | 3 s | 1 |
| `tree_fall` | The tree gives its life for you | big sad tree crash with a fading light tone | 3 s | 1 |
| `jar_drain_loop` | NEW: the Fairy shut in a jar by the final boss, her light flowing into it | eerie magical draining hum, glassy, slow pulse | 4 s LOOP | 1 |
| `jar_break` | NEW: the boss is beaten, her jar shatters, she is free | glass jar shattering with a bright magical release chime | 1.5 s | 1 |
| `sparkle_rain_loop` | The ending: she showers you in green light | soft sparkle rain, magical, gentle | 5 s LOOP | 1 |
| `green_protector_sting` | The ending title THE GREEN PROTECTOR | short heroic magical title sting, warm brass and bells | 3 s | 1 |

Also for her board (optional, small): `board_open` (magical shimmer 0.5 s), `arrow_tick` (soft tick 0.2 s),
`card_pick` (sparkle pick 0.4 s). Her calls from the jar reuse `FairyHelp` and `FairyCallHelp` (yours, unused so far).

## Priority 1: the biggest difference (about 40 files)

### Voices (yours or ElevenLabs, see above)

| File | Moment | Line | Where it goes |
|---|---|---|---|
| `fairy_please_find_me` | Taking a room seed piece | "Please... find me..." (weak, far away) | Story Seed Settings > Voice (now a stand-in: Fairy_Call_2) |
| `fairy_good_luck`, `fairy_have_fun`, `fairy_be_careful` | She leaves after you pick on her board | "Good luck!" / "Have fun!" / "Be careful out there!" | Story Fairy Settings > Send Off Lines (empty now) |
| `fairy_hello` | Her board opens (she waves) | "Hi!" / "Hello, Knight!" | new slot, her hello |
| `fairy_thank_you` | After the final boss, while she cheers | "You did it! Thank you!" | FairyHealth Win (now only fairy_reward) |
| `fairy_talk_1`, `fairy_talk_2` | After the boss, she talks 3 s before teleporting | your words (e.g. why she was caught, what the Green is) | FairyHealth Win 2 (silent now) |
| `voice_fight` | The walls drop and a fight starts (4 rooms) | "Fight!" | Story Seed Settings > Fight Voice (empty) |
| `voice_protected_the_green` | The ending, THE GREEN PROTECTOR | "You protected the Green." | Story Seed Settings > End Voice (empty) |

Also on your list from before (not ElevenLabs): re-record the Knight's 3 opening lines "with a bigger voice"
(you said good enough for now), and re-export `welcome_back_1/2` uncut (both start mid-word: "elcome back").

### Sound effects

| File | Moment | ElevenLabs prompt | Length | Takes |
|---|---|---|---|---|
| `player_hurt_light` | A bug bite or sting gets through (now SILENT) | soft punchy body hit with a light cloth-and-armor rattle, no voice | 0.4 s | 3 |
| `player_hurt_heavy` | Boss punch, slam, bull charge, bomb (now a bite sound) | heavy blunt body impact, armor clank, low thump | 0.7 s | 2 |
| `heartbeat_loop` | Low health (now a thump the code makes up) | slow tense heartbeat, lub-dub, muffled, close | 1.0 s LOOP | 1 |
| `bomb_beep` | Bee Ticking Bomb (now a made-up tone) | cartoon bomb timer beep, single short bright beep | 0.2 s | 1 |
| `bee_buzz_loop` | Every bee flying (now SILENT) | small cartoon bee wings buzzing, steady, close | 2 s LOOP | 1 |
| `bug_skitter_loop` | Spiders and roaches walking (now SILENT; you need it to hear them behind you) | tiny insect legs skittering fast on a wooden floor | 2 s LOOP | 2 |
| `explosion_small` | Books, bottles, flip-flops, boxes, smoke ball | small cartoon pop explosion with a puff | 0.8 s | 2 |
| `explosion_medium` | Held pumpkin, kamikaze bee, gun overheat, bowling pumpkin | medium cartoon explosion, pumpkin splatter | 1.2 s | 2 |
| `explosion_big` | Ticking Bomb, Explosive Fist, Blast Skills | big boomy cartoon explosion with debris | 2 s | 2 |
| `bat_hit` | Baseball bat hits (now the broom's sounds) | wooden baseball bat cracking into a creature, chunky | 0.5 s | 3 |
| `broom_hit` | Broom hits (now SILENT on the broom) | broom bristles thwapping, dusty swat | 0.4 s | 2 |
| `weapon_swing` | Any weapon swung fast | quick air swish of a swung stick | 0.3 s | 3 |
| `fairy_teleport_blink` | The Fairy's green flash (now her fly-away line) | magical sparkly blink teleport, short shimmer zap | 0.6 s | 2 |
| `stage_swap_poof` | The smoke at every stage change (now Wall_Shatter_2 about 15 times) | magic smoke poof, soft whoosh with sparkles | 0.8 s | 2 |
| `round_start` | A gate round starts (slot empty) | short fanfare drum hit and horn, "go" | 1.2 s | 1 |
| `round_won` | A gate round is won (slot empty) | happy short victory jingle, bells | 2 s | 1 |
| `time_up_buzzer` | A round's time runs out | cartoon game-show time-up buzzer | 0.8 s | 1 |
| `ui_notice_pop` | A notice card appears and the game pauses (now SILENT) | gentle UI pop-in chime | 0.4 s | 1 |
| `ui_menu_open`, `ui_menu_close` | Pause menu (now SILENT) | soft UI whoosh open / close | 0.4 s | 1 each |

---

## Priority 2: enemies, bosses, stages (about 60 files)

### Enemies

| File | Moment | ElevenLabs prompt | Length | Takes |
|---|---|---|---|---|
| `roach_face_nibble` | Roach on your face bites every ~2 s (now SILENT) | very close wet insect nibble, munchy crunch | 0.4 s | 3 |
| `roach_slapped_off` | Slapping the roach off your face | insect squeal as it is flung away | 0.5 s | 1 |
| `bug_spawn` | Bugs climbing out, minions arriving, bugs spat out | dirt scrabble and a small pop-out | 0.6 s | 2 |
| `miniboss_chaos_rise` | A mini boss is reborn as chaos (now SILENT) | ominous rising whoosh into a demonic roar | 2 s | 1 |
| `chaos_aura_loop` | The red chaos aura | low evil crackling hum | 2 s LOOP | 1 |
| `chaos_denied` | Too-fast kill held at 1 health | metallic "denied" clang with a hiss | 0.6 s | 1 |
| `miniboss_death` | A mini boss dies (now the tiny bug_die) | big insect death screech and heavy collapse | 1.5 s | 2 |
| `boss_summon` | A boss calls its minions (now the notice squeak) | loud insect summoning screech, echo | 1.2 s | 1 |
| `roach_charge_loop` | Bull Charge gallop | fast heavy insect legs galloping | 1.5 s LOOP | 1 |
| `roach_charge_crash` | Bull Charge hits a wall (now SILENT) | crunchy body crash into wood, dazed chirps | 1 s | 1 |
| `spider_web_windup` | Spider gathers a web shot | silk stretching, tense creak | 0.8 s | 1 |
| `spider_web_spit` | Web shot (now a card-spit sound) | wet sticky "thwip" spit | 0.4 s | 2 |
| `spider_giant_web` | Giant Web launch | heavy sticky web whoosh launch | 1 s | 1 |
| `web_splat` | Web ball hits you (now a bubble pop) | sticky goopy web splat | 0.5 s | 2 |
| `web_wrap`, `web_tear` | You get fully webbed / break free (now SILENT) | silk wrapping tight creak / web tearing snap | 0.8 s | 1 each |
| `poison_tick` | Each poison tick (now SILENT) | small toxic sizzle hiss | 0.3 s | 2 |
| `bee_kamikaze_dive` | Kamikaze bee dives (now a weapon beep) | angry rising insect whine diving | 1.2 s | 1 |
| `bee_sting_shot`, `bee_sting_hit` | Poison sting dart / hit (now bug_Attack for both) | tiny dart "thwip" / sharp stab with venom hiss | 0.3 s | 1 each |
| `bee_bomb_arm`, `bee_bomb_fizzle` | Ticking Bomb arms / is killed in time | fuse lighting hiss / bomb fizzles out "pfft" | 0.6 s | 1 each |
| `bee_wings_snap` | A bee's wings break off | dry crack, buzzing cuts out | 0.5 s | 1 |
| `bug_dodge`, `bug_flee`, `bug_snap_miss` | Bug dodges / flees / bites air | quick skitter whoosh / panicked squeak / jaws snapping shut | 0.3-0.5 s | 1 each |
| `bug_hide_pumpkin` | Roach hides in a pumpkin (now its death sound) | wet rustle squelching into a pumpkin | 0.6 s | 1 |
| `white_bug_appear` | Rare white bug appears (now SILENT) | soft magical twinkle shimmer | 0.8 s | 1 |
| `slime_bite`, `spider_pounce_land` | Slime bite wind-up / spider pounce landing (slots empty) | gooey slime chomp / soft heavy spider landing thump | 0.5 s | 1 each |
| `bug_blocked` | Bee or roach bite blocked by fist or weapon (now `bone_bonk_1` as a stand-in, optional) | hard chitin clack on armor | 0.3 s | 2 |

### Pumpkins and bosses

| File | Moment | ElevenLabs prompt | Length | Takes |
|---|---|---|---|---|
| `boss_clap` | Pumpkin boss claps (now SILENT) | big hollow slow giant clap | 0.8 s | 1 |
| `boss_cyclone_loop` | Cyclone spin | whirling wind with floor scraping | 2 s LOOP | 1 |
| `boss_butt_crack` | Its body cracking under hits | pumpkin rind creaking and cracking | 0.6 s | 3 |
| `boss_dig_rumble` | Boss sinks into / climbs out of the floor | dirt shifting rumble | 1.5 s | 1 |
| `boss_hop` | Boss hops far away | cartoon heavy boing | 0.5 s | 1 |
| `pumpkin_lob` | Thrown bug pumpkin in flight | lobbed object whoosh | 0.8 s | 1 |
| `runner_hop`, `runner_pant`, `runner_caught` | Runner pumpkins hop / are tired / are caught | small boing / cartoon panting loop / little "eep" squeak | 0.3-1 s | 2 / LOOP / 1 |
| `pumpkin_thunk` | Hits on tree pumpkins and the tree (now wood/rock) | hollow pumpkin thunk | 0.4 s | 3 |
| `fat_giggle` | Fat Pumpkin laughs (empty) | greedy chubby giggle, cartoon creature | 1 s | 3 |
| `fat_angry` | Fat Pumpkin turns red and fumes (empty) | angry huff with steam hiss | 1 s | 1 |
| `fat_chomp` | Fat Pumpkin bite (empty) | big wet chomp with a springy hop | 0.6 s | 1 |
| `fat_slam`, `fat_hit` | Giant stomp / hits on it (now chest_drop, bone_bonk) | heavy pumpkin body-slam thud / squishy pumpkin thunk | 0.6 s | 1 / 2 |
| `start_pumpkin_land` | The first pumpkin lands on the Fairy's spot (no impact sound) | heavy thud with a ground shake | 1 s | 1 |
| `start_pumpkin_crack` | The first pumpkin cracks at half health (SILENT) | sharp gourd crack crunch | 0.5 s | 1 |
| `jail_seal_break` | The Fairy's purple jail breaks (SILENT) | glassy magic seal cracking, purple whoosh | 1.5 s | 1 |
| `miniboss_pop_burst` | Mini boss bursts out of the laughing pumpkin (now a tiny pop) | big wet pumpkin burst | 1 s | 1 |

### Gates and stages

| File | Moment | ElevenLabs prompt | Length | Takes |
|---|---|---|---|---|
| `last_seconds_tick` | Last 10 s of a gate round (SILENT) | clock tick, tense | 0.3 s | 1 |
| `gate3_alarm` | Gate 3 song clock running out | rising alarm siren, cartoon | 2 s | 1 |
| `portal_in`, `portal_arrive`, `portal_out` | Into a gate / arriving / Gate Exit (mostly SILENT) | portal whoosh in / arrival shimmer / suction whoosh out | 1 s | 1 each |
| `portal_open` | A path gate opens after the 3 s countdown (now a wolf howl) | magical portal opening chime and whoosh | 1.5 s | 1 |
| `candy_pop` | Every candy out of a fountain or the gate 3 candy rain (SILENT) | small light candy pop, plastic tick | 0.2 s | 4 |
| `bowl_release`, `bowl_roll_loop`, `bowl_pins` | Bowling: let go / ball rolling 20 m / pins knocked (SILENT) | heavy ball release thunk / rolling rumble on wood / squishy slime pins clattering | 0.4 s / 2 s LOOP / 1 s | 1 each |
| `field_shrink_loop`, `field_collapse` | Gate 3 field shrinking / collapsing (SILENT) | low ground creak rumble / big crumbling collapse crash | 2 s LOOP / 2 s | 1 each |
| `ball_drop` | Gate 3 balls dropping in from the sky (SILENT) | falling whistle then heavy thud | 1.5 s | 1 |
| `hole_spit` | A hole spits out a gear seed | "pthoo" spit and rising whizz | 0.8 s | 1 |
| `seedlock_pop`, `seedlock_need_more` | Price board pops up / "You need N more" | UI pop / soft "can't" buzz | 0.4 s | 1 each |
| `whack_mallet_pop`, `whack_grab` | Mallets appear / you grab one | pop / solid wooden grab thunk | 0.3 s | 1 each |
| `whack_machine_rise` | Whack machine rises and sinks (now a card spit) | mechanical rising rumble with gears | 1.5 s | 1 |
| `plus_time_ding`, `score_fanfare` | Bonus "+seconds" / the end board (both now confirmation_002) | bright "+time" ding / short score fanfare | 0.4 s / 2 s | 1 each |
| `cauldron_plop`, `card_flick`, `fire_whoomph`, `slime_boss_roar` | Cauldron: ingredient in / hat spits a card / fuel / mini bosses come | liquid plop splash / card flick / fire whoomph / gurgly slime roar | 0.4-1.5 s | 1 each |
| `giant_ball_land`, `giant_ball_hurt` | Gate giant pumpkin ball lands / gets hurt | heavy pumpkin thud / squelchy pumpkin hurt | 0.8 s | 1 / 2 |
| `room_cleared` | Last bug of a wave dies, before the mini boss | short "room cleared" sting | 1.2 s | 1 |
| `tree_grow`, `tree_bite`, `tree_fall` | Pumpkin tree grows / is bitten / dies | magical bloom with creak / bark bite / big sad tree crash with fading light tone | 1-3 s | 1 each |
| `leaf_rustle` | Leaves fall on every tree hit (SILENT) | leaves rustling and falling | 0.8 s | 2 |
| `pumpkin_plop` | Pumpkin rooms: pumpkins rain from the ceiling (SILENT) | thumpy pumpkin plop on wood | 0.4 s | 3 |

---

## Priority 3: the world, the story, small details (about 45 files)

### Rooms and the world

| File | Moment | ElevenLabs prompt | Length | Takes |
|---|---|---|---|---|
| `room_unfold` | Room walls unfold and the roof flies in (SILENT) | wooden pop-up book unfolding creak | 1.2 s | 2 |
| `room_fold` | Walls fold and the roof flies off | wooden folding creak and whoosh | 1.2 s | 1 |
| `roof_land` | The roof lands | solid wooden thud | 0.6 s | 1 |
| `quake_walls_sink` | Earthquake, walls sinking as a fight starts | rumble with walls grinding down | 2.5 s | 1 |
| `room_arrive` | Living room / rooftop arrive (now a bubble pop) | rising rumble ending in a stone thump | 2 s | 1 |
| `stone_gates_rise`, `gate_creak`, `spawn_circle_hum` | Rooftop: 4 stone gates rise / open / spawn circles open | stone grinding / gate creak / low portal hum LOOP | 1-2 s | 1 each |
| `door_slam`, `door_break` | Door slams (now a sofa hit) / breaks (now a pumpkin explosion) | wooden door slam / wood splinter crash | 0.8 s | 1 each |
| `lamp_on` | Secret room lamp comes on (slot empty) | lamp click and soft electric hum | 0.8 s | 1 |
| `footstep_wood`, `footstep_stone` | Walking (SILENT) | soft footstep on wooden floor / on stone | 0.3 s | 4 each |
| `teleport_move` | Teleport locomotion | short soft whoosh | 0.4 s | 1 |

### Ambience (none exists today; all LOOP, 20-30 s)

| File | Where | ElevenLabs prompt |
|---|---|---|
| `amb_storage_room` | Storage room / the lair | dusty storage room at night, faint creaks, tiny scratching inside the walls |
| `amb_bedroom` | Bedroom | quiet toy-filled bedroom at night, soft clock ticking, light wind at the window |
| `amb_livingroom` | Living room | cozy living room at night, crackling fireplace, house settling |
| `amb_playroom` | Playroom | quiet playroom at night, faint creaky toys, gentle eerie air |
| `amb_rooftop` | Rooftop | windy rooftop at night, distant crickets |
| `amb_playground` | Gate 1 | playground at night, crickets, soft breeze in grass |
| `amb_bowling_sky` | Gate 2 | high open sky, soft steady wind |
| `amb_valley` | Gate 3 | grassy valley at night, crickets and frogs |
| `amb_dark_walk` | The first dark walk | low dark drone, cold wind, empty space |

### Story moments

| File | Moment | ElevenLabs prompt | Length |
|---|---|---|---|
| `light_return_swell` | "The floor shows up" after the dark walk | warm rising magical swell | 2.5 s |
| `lamp_flicker` + `cold_gust` | Laugh chill: the lamp stutters, the room turns cold | electric lamp flicker buzz / cold breathy gust | 1.5 s each |
| `seed_chime`, `seed_whoosh`, `seed_merge`, `seed_plunge` | Seed pieces fly out, join, dive into the dirt (SILENT) | bright chime / sparkle whoosh trail / shimmering merge / soft earthy plunge | 0.5-1.5 s |
| `sparkle_rain_loop` | The green sparkle shower at the end | soft sparkle rain, magical, LOOP | 5 s |
| `green_protector_sting` | Title of the ending | short heroic magical title sting | 3 s |
| `fairy_eek` (voice) | She sees the pumpkin falling (startled) | a tiny gasp "Eek!" | 0.5 s |

### Player and UI details

| File | Moment | ElevenLabs prompt | Length | Takes |
|---|---|---|---|---|
| `perfect_jab` | A perfect jab (same sound as any jab now) | bright crisp "ping" crack on top of a punch | 0.4 s | 1 |
| `hot_hands_on`, `hot_hands_loop`, `hot_hands_flicker`, `hot_hands_off` | Red hands start / burn / last second / cool down | fire whoomp / sizzle LOOP / crackle / steam hiss | 0.5-2 s | 1 each |
| `far_grab_pull`, `far_grab_catch` | Far grab pulls a prop in (SILENT) | tractor-beam "fwoop" / soft catch | 0.6 s | 1 each |
| `launch_whoosh`, `land_thud` | You get launched / land | big whoosh / floor thud | 1 s | 1 each |
| `down_slowmo`, `revive_burst` | You go down / get up again | slow-motion fall whoosh / energy burst | 1.5 s | 1 each |
| `coin_spin`, `clover_chime` | Revive coins spin / clover (now a shop sound) | coin whirring and slowing / lucky magical chime | 1.5 s | 1 each |
| `sword_parry` | Legendary sword blocks a bite (slot empty) | sword parry clang | 0.5 s | 1 |
| `smoke_fizzle` | Smoke ball misses | puff fizzle | 0.5 s | 1 |
| `unlock_fanfare` | NEW PUNCH / NEW POWER cards (SILENT) | short unlock fanfare | 1.5 s | 1 |
| `stat_up`, `no_points` | Level-up menu (now shop sounds) | "stat up" ding / soft deny buzz | 0.4 s | 1 each |
| `board_open`, `arrow_tick`, `card_slide`, `card_pick` | The Fairy's board (SILENT) | magical shimmer / soft tick / paper card slide / sparkle pick | 0.3-0.6 s | 1 each |
| `ui_blip` | Saved sign, wrist inventory, shop close, slot hover, "?" box reveal | soft UI blip | 0.2 s | 3 |

---

## Fixed with clips you already have (DONE 2026-11-25)

Menu **Tools > 429 Game > Audio > Fill Silent Sounds (Clips We Have)** (re-runnable; prefabs only, no scene change)
plus three small code changes. Checked in the editor, not in the headset.

1. ~~The Fairy's jail breaking is silent~~: it was fine. Its clip is `glass_break_2`; the check misread Unity 6,
   which keeps an AudioSource's clip in a new field.
2. **Bedroom_Book_5** hit (its Health "Compare Health" played no clip): now `bubble_pop_1`, like the other books.
3. **Stone_Cube2_Drop and Rooftop_Pole_Light_Drop** (the random pick was never stored): the pick now goes into the
   variable their Audio Play plays (`Wall_Shatter_1/2`, `wood_furniture_break_2/3`).
4. **The pumpkin boss's hurt yelp:** new Hurt Sounds list = `ouch_pumpkin_1-4` at random, never the same twice
   in a row (PumpkinBossBrain; its setup menu sets the list too).
5. **Bee and roach blocked bites:** `bone_bonk_1` on Bee_Smarter and Cockroach_Smarter; all 9 variants inherit it
   (the bull-charge boss keeps its own).
6. **The Boong gun's shot:** Overheat has a new Shot Sound, played on every trigger pull: `elon_boong_shoot`.
7. **Gate 3's Black Pumpkin Ball laugh and burst:** played wide now (heard to about 80 m, full volume within 8 m).
8. Not done: the spare clips (`FairyHelp`, `FairyCallHelp`, `welcome_back_2`, `pumpkin_roar_big`) have no clear
   slot, and `Someone_gets_launche` would double up with the giant smash, which already plays it when you're
   launched. They wait for the new sounds (`launch_whoosh` etc.).

Two sounds break the "no made-up sounds" rule today and should be replaced first: the low-health
**heartbeat** (the code builds a thump when the slot is empty) and the Ticking Bomb's **bomb_beep**
(synthesized on 10-24). Both are in Priority 1.

## Priority 1 wired (DONE 2026-11-25 late)

Menu **Tools > 429 Game > Audio > Wire ElevenLabs Sounds (P1)** (re-runnable: a second run says "everything was
already wired"). Prefabs and settings are saved by it; the scene parts need **2 Game Scene saved**. Editor-tested,
not in the headset. First (user OK: volume and cutting the silent start only) the quiet takes were raised to a -1 dB
peak, takes of one sound evened out, and 5 late starts cut; originals in
`Documents/429R_claude_backups/2026-11-25/audio_originals/`.

- **Voices:** seed piece = `fairy_please_find_me` or the "I am here" take (random); Fight; the ending line. Fairy: hello
  (2 takes, not again within 20 s), send-off (3; she now stays until the line is said), thank you (FairyHealth Win,
  0.6 s in), talk 1 then 2 (Win 2 waits for both, about 12 s), teleport blink (2 takes).
- **Player:** hurt light / heavy on the XR Origin (Damage takes 6-9 = light, DamageHeavy 40-50 = heavy; from 15);
  heartbeat = one lub-dub of `heartbeat_loop` per beat, faster as health drops.
- **Bugs:** skitter while walking (louder the faster), buzz while flying; `Resources/Bug Sounds` for every Spider
  Brain bug.
- **Weapons:** swing swish on the broom, bat, pan, 3 Boong hammers, pillow and tool hammer; bat and broom hits.
- **Explosions:** small 1 = books, flip-flops, smoke ball; small 2 = bottles, glow jar, box bombs, thrown things;
  medium 1 = held pumpkins, Boong gun; medium 2 = kamikaze bees, bowling pumpkin; big 1 = Ticking Bomb (+ yellow slime),
  Weapon Master; big 2 = Explosive Fist. The setup menus that overwrite now name the new clips.
- **bomb_beep2** replaces the made-up `bomb_beep` everywhere (bee boss, yellow slime, picture puzzle tick).
- **Stage-change poof:** the 4 smokes GAMESTAGES switches on played `bubble_pop_1` (not Wall_Shatter_2 as the table
  above says): now `stage_swap_poof_1/2`. Real breaks keep Wall_Shatter.
- **UI:** notice pop, pause menu open/close. **Gates 1-2 (GateRun):** round start, round won, time-up buzzer (new slot).
- Also switched (user "Yes, switch them"): smoked boongs = medium 1, the bonus round's time-up blast = big 2, the
  giant slime's burst = big 1. The old bang (`explosion-312361.wav`) is no longer played anywhere (only an unused list
  in Rooftop_Base_drop's FSM still names it).
- Not used: `fairy_hello_there.mp3` and `fairy_thank_you.mp3` (same takes as the .wav files), the five `- Copy` files.

## Optional

- **Gate 3 song:** the notes say "a real 5-minute song" (now two songs sped up). ElevenLabs Music can make
  songs, but on 10-28 you said "stop creating more songs for now".
- **The Paint Bees round and Daruma** are fully sounded but not in the current game flow.
