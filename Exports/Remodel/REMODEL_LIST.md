# 429 remodel list: the main game, stage by stage (2026-11-16)

Made from the game itself: every prefab that GAMESTAGES, the room sets, the enemy managers and the boss spawn, and everything
those spawn in turn (broken pieces, drops, candies, bugs). Each prefab is listed once, in the first stage where it appears.
Effects, lights, spawners and other prefabs with no model are left out. Style: the concept art in `Exports/*_Concept/`
(`Rooftop_Concept/TEXTURE_STYLE.md` says how: chunky bevelled shapes, flat colours with soft gradients, soft AO).

- **model:** the file(s) its look comes from today (in `Assets/Models/...`). One file can serve several prefabs: remodel it once.
- **broken:** the pieces it breaks into. Those have to be remade from the NEW model (cut it up after it's done).
- `(same model as X)`: already covered by X's model.

## 1 Rooftop: tutorial start (16 prefabs, 30 new model files)

### Rooms & big pieces
- [ ] **Rooftop_Base_Z** — model: Rooftop_Base_Z.fbx, Stone_Pumpkin_2.fbx, Rooftop_Base_Z_Lid.fbx, Lid Running Light Ring.asset — broken: Rooftop_Base_Z_Lid_Drop.fbx, Rooftop_Base_Z_Round_Gate_Drop.fbx, Rooftop_Base_drop.fbx, Stone_Pumpkin_Drop.fbx
- [ ] **Skydome** — model: Skydome_Part_Cell.fbx, Star_Yellow_Particle.asset, Skydome.fbx

### Characters
- [ ] **Fairy** — model: Fairy_Girl.fbx
- [ ] **Knight** — model: Glove_Placeholder_Mitt_R.asset, Glove_Placeholder_Cuff_R.asset, Glove_Placeholder_Cuff_L.asset, Glove_Placeholder_Mitt_L.asset, Knight.fbx

### Furniture & big props
- [ ] **Door_1-4_Set** — model: Door_1_Frame.fbx

### Small breakables & props
- [ ] **Candle_Fat** — model: Candle_Fat.fbx
- [ ] **Stone_Cube_Dice 1** — model: Stone_Cube2.fbx, pumpkin_get_hit.fbx, pumpkin_bleh.fbx, pumpkin_break.fbx — broken: Stone_Cube2_Drop.fbx
- [ ] **Stone_Cube_Dice 2** (same model as Stone_Cube_Dice 1)
- [ ] **Stone_Cube_Dice 3** (same model as Stone_Cube_Dice 1)

### Pumpkins
- [ ] **Pumpkin_0** — model: pumpkin_evil.fbx
- [ ] **Pumpkin_00** — model: pumpkin_0.fbx, eye_wear.fbx — broken: eye_wear.fbx, pumpkin_0_drop.fbx
- [ ] **pumpkin_growing** — model: pumpkin_growing.fbx
- [ ] **Pumpkin_Tree** — model: Pumpkin_Tree.fbx, Stone_Pumpkin.fbx — broken: Pumpkin_Tree_Drop.fbx
- [ ] **StartGamePumpkin** — model: pumpkin_0_drop.fbx, 429.fbx, Armour_Stand_Sign1.fbx (same model as Pumpkin_00)

### Enemies & bosses
- [ ] **Pumpkin_Boss** — model: Pumpkin_Boss.fbx

### Chests, seeds & candy
- [ ] **The_Seed_Start** — model: Magical_Seed.fbx

## 2 Mini bedroom (44 prefabs, 42 new model files)

### Rooms & big pieces
- [ ] **Bedroom_Base_Z** — model: Bedroom_Base_Z_Door.fbx, Bedroom_Base_Z_Door_2.fbx, Butterfly_and_Flower.fbx, Bedroom_Base_Z.fbx

### Furniture & big props
- [ ] **Banner_Hang** — model: Banner_Hang.fbx
- [ ] **Bedroom_Coffer+Magic_Hats** — model: Bedroom_Long_Table.fbx
- [ ] **Bedroom_Matt** — model: Bedroom_Matt.fbx
- [ ] **Bedroom_pumpkin_music_player** — model: pumpkin_music_player.fbx — broken: pumpkin_music_player_drop.fbx
- [ ] **Bedroom_Stuffs_Shelf** — model: Bedroom_Stuffs_Shelf.fbx — broken: Bedroom_Stuffs_Shelf_Drop.fbx
- [ ] **Livingroom_Chair** — model: LivingRoom_Chair.fbx — broken: Bedroom_Bed_Stool_Drop.fbx
- [ ] **Livingroom_Table_2_small** — model: Livingroom_Table_2_small.fbx — broken: Livingroom_Table_2_Drop.fbx
- [ ] **MiniBedroom_Shelf_01** — model: Shelf_01.fbx — broken: Shelf_01_Drop.fbx
- [ ] **Picture_Hang_4** — model: Picture_Hang_4.fbx
- [ ] **Wood_Box** — model: Wood_Box.fbx — broken: Wood_Box_Drop.fbx
- [ ] **Wood_Box_Weapon_Main** (same model as Wood_Box) — broken: Wood_Box_Drop.fbx

### Small breakables & props
- [ ] **Candle_Land** — model: Candle_Land.fbx
- [ ] **Candle_Land_2** — model: Candle_Land_2.fbx
- [ ] **Hat_Black** — model: Hat_Black.fbx
- [ ] **Hat_Blue** — model: Hat_Coal.fbx, Hat_Green.fbx
- [ ] **Hat_Green** (same model as Hat_Blue)
- [ ] **Paper_Box_1** — model: Paper_Box_1.fbx — broken: Paper_Box_1_Drop.fbx
- [ ] **Paper_Box_2** — model: Paper_Box_2.fbx — broken: Paper_Box_2_Drop.fbx
- [ ] **Paper_Box_3** — model: Paper_Box_3.fbx — broken: Paper_Box_3_Drop.fbx
- [ ] **Paper_Box_4** (same model as Paper_Box_3) — broken: Paper_Box_3_Drop.fbx
- [ ] **Paper_Box_5** (same model as Paper_Box_3) — broken: Paper_Box_3_Drop.fbx
- [ ] **Skull_Chaos** — model: Skull.fbx
- [ ] **Skull_Stuffs** — model: Skull_Blue.fbx

### Pumpkins
- [ ] **Pumpkin_1** — model: beanie.fbx (same model as Pumpkin_0)
- [ ] **Pumpkin_1_Chaos** (same model as Pumpkin_0, Pumpkin_1) — broken: pumpkin_evil_0_drop.fbx
- [ ] **Pumpkin_6 1** — model: Skull_Mask.002.fbx (same model as Pumpkin_0)
- [ ] **Pumpkin_6 2** — model: Skull_Mask.003.fbx (same model as Pumpkin_0)
- [ ] **Pumpkin_On_Tree** — model: pumpkin_1.fbx — broken: pumpkin_2_Drop.fbx
- [ ] **Skull_Pumpkin** (same model as Pumpkin_0)

### Weapons & throwables
- [ ] **Bus_Boong** — model: Boong_V01.fbx — broken: Boong_V01_Drop.fbx
- [ ] **Bus_Boong_Bullet** — model: Work_Table_Chair.fbx — broken: Skull_drop.fbx
- [ ] **Hammer_Broom** — model: Broom.fbx — broken: Broom_Broken_Part.fbx

### Enemies & bosses
- [ ] **Bowling_Pumpkin_Enemy** — model: pumpkin_bowling_enemy.fbx
- [ ] **Fat_Pumpkin_Enemy** — model: pumpkin_fat.fbx — broken: pumpkin_0_drop.fbx
- [ ] **Spider_Smarter** — model: Spider_Smol_&_Anims.fbx
- [ ] **Spider_Web_Ball** — model: New_Web_Ball.fbx

### Chests, seeds & candy
- [ ] **Candy_2** — model: Candy_2.fbx
- [ ] **Candy_4** — model: Candy_4.fbx
- [ ] **Candy_5** — model: Candy_5.fbx
- [ ] **Candy_7** — model: Candy_7.fbx
- [ ] **Candy_8** — model: Candy_8.fbx
- [ ] **Seed_Shield** — model: Magical_Seed_Bad.fbx
- [ ] **Treasure_Chest_and_Seed_Shield** — model: Treasure_Chest_and_Anims.fbx — broken: Treasure_Chest_Drop.fbx

## 3 Bedroom (38 prefabs, 35 new model files)

### Furniture & big props
- [ ] **Bedroom_Bed** — model: Bedroom_Bed_Sheet.fbx, Bedroom_Bed.fbx, Bedroom_Bed_Pillow.fbx — broken: Bedroom_Bed_Drop.fbx, Bedroom_Bed_Sheet.fbx
- [ ] **Bedroom_Bed_Stool** — model: Bedroom_Bed_Stool.fbx — broken: Bedroom_Bed_Stool_Drop.fbx
- [ ] **Bedroom_Clock** — model: BR_Clock.fbx — broken: BR_Clock_Drop.fbx
- [ ] **Bedroom_Coffer** — model: Bedroom_Coffer.fbx — broken: Bedroom_Coffer_Drop.fbx
- [ ] **Bedroom_Matt_2** — model: Bedroom_Matt_2.fbx
- [ ] **Bedroom_Shelf_01** (same model as MiniBedroom_Shelf_01) — broken: Shelf_01_Drop.fbx
- [ ] **Bedroom_Shelf_03** — model: Shelf_03.fbx — broken: Shelf_03_Drop.fbx
- [ ] **Bedroom_Work_Table** — model: Work_Table.fbx — broken: Work_Table_Drop.fbx
- [ ] **Bedroom_Work_Table_Chair** (same model as Bus_Boong_Bullet)
- [ ] **Picture_Hang_3** — model: Picture_Hang_3.fbx
- [ ] **Shelf_Hang_Wood** — model: Shelf_Hang.fbx
- [ ] **Shelf_Hang_Wood_2** — model: Shelf_Hang_2.fbx
- [ ] **Spider_Web** — model: Web_Ball.fbx, Spider_Web.fbx

### Small breakables & props
- [ ] **Bedroom_Book_11** — model: Bedroom_Book_11.fbx
- [ ] **Bedroom_Book_12** — model: Bedroom_Book_12.fbx
- [ ] **Bedroom_Book_13** — model: Bedroom_Book_13.fbx
- [ ] **Bedroom_Book_2** — model: Bedroom_Book_2.fbx — broken: book_drop.fbx
- [ ] **Bedroom_Book_3** — model: Bedroom_Book_3.fbx
- [ ] **Bedroom_Candy_Basket** — model: Candy_Basket.fbx, Candy_Pumkin.fbx, Candy_1.fbx — broken: Candy_Basket_Drop.fbx
- [ ] **Empty_Bottle_1** — model: Empty_Bottle_1.fbx — broken: Empty_Bottle_1_Drop.fbx
- [ ] **Empty_Bottle_2** — model: Empty_Bottle_2.fbx — broken: Empty_Bottle_2_Drop.fbx
- [ ] **Empty_Bottle_3** — model: Empty_Bottle_3.fbx — broken: Empty_Bottle_3_Drop.fbx
- [ ] **Empty_Bottle_4** — model: Empty_Bottle_4.fbx — broken: Empty_Bottle_4_Drop.fbx
- [ ] **Empty_Bottle_5** — model: Empty_Bottle_5.fbx — broken: Empty_Bottle_5_Drop.fbx
- [ ] **Skull_2** — model: Skull_2.fbx
- [ ] **Skull_3** — model: Skull_3.fbx
- [ ] **Web_Ball_On_Web** (same model as Spider_Web)

### Pumpkins
- [ ] **Pumpkin_2** — model: Sleeping_Hat.fbx (same model as Pumpkin_0)
- [ ] **Pumpkin_2_Chaos** (same model as Pumpkin_0, Pumpkin_2)
- [ ] **Pumpkin_5** — model: Witch_Hat.fbx (same model as Pumpkin_0)
- [ ] **Pumpkin_Candle** — model: pumpkin_2.fbx (same model as StartGamePumpkin)

### Weapons & throwables
- [ ] **Flip_Flop_L** — model: flip_flop.fbx
- [ ] **Flip_Flop_R** (same model as Flip_Flop_L)
- [ ] **Hammer_Pillow** (same model as Bedroom_Bed)

### Enemies & bosses
- [ ] **Spider_Boss_PumpkinButt** — model: pumpkin_crown.fbx, pumpkin_4.fbx (same model as Skull_Chaos)

### Chests, seeds & candy
- [ ] **Seed_Head** (same model as Seed_Shield)
- [ ] **Treasure_Chest_and_Seed_Head** (same model as Treasure_Chest_and_Seed_Shield)
- [ ] **Treasure_Chest_PUMPKIN_EXTRA** (same model as Treasure_Chest_and_Seed_Shield)

## 4 Living room (36 prefabs, 33 new model files)

### Rooms & big pieces
- [ ] **Livingroom_Base_Z** — model: Livingroom_Base_Z_Roof_Sub.fbx, Livingroom_Base_Z.fbx (same model as Bedroom_Base_Z) — broken: Livingroom_Base_Z_Roof_Sub_Drop.fbx

### Furniture & big props
- [ ] **Brick_Wall_Breakable** — model: Brick_Wall_Breakable.fbx — broken: Brick_Wall_Breakable_drop.fbx
- [ ] **Livingroom_Book_Shelf** — model: Book_Shelf.fbx (same model as Spider_Web) — broken: Book_Shelf_Drop.fbx
- [ ] **LivingRoom_Matt** — model: LivingRoom_Matt.fbx
- [ ] **Livingroom_Sofa_Couple** — model: Livingroom_Sofa_Couple.fbx — broken: Livingroom_Sofa_Couple_Drop.fbx
- [ ] **Livingroom_Sofa_Small** — model: Livingroom_Sofa_Small.fbx — broken: Livingroom_Sofa_Small_Drop.fbx
- [ ] **LivingRoom_Stool** — model: LivingRoom_Stool.fbx — broken: LivingRoom_Stool_Drop.fbx
- [ ] **Livingroom_Table_2** — model: Livingroom_Table_2.fbx
- [ ] **Livingroom_Table_2+Magic_Hat** (same model as Livingroom_Table_2)
- [ ] **Livingroom_Table_3** — model: Livingroom_Table_3.fbx — broken: Livingroom_Table_3_Drop.fbx
- [ ] **Livingroom_WhaleCome_Matt** — model: WhaleCome_Matt.fbx
- [ ] **Picture_Hang_10** — model: Picture_Green_Leaf.fbx
- [ ] **Picture_Hang_5** — model: Picture_Hang_5.fbx
- [ ] **Picture_Hang_6** — model: Picture_Hang_6.fbx
- [ ] **Picture_Hang_7** — model: Picture_Hang_7.fbx
- [ ] **Picture_Hang_8** — model: Picture_Hang_8.fbx
- [ ] **Pie_Knight_Shield** — model: Pie_Knight_Shield.fbx, Pie_Knight_Shield_Piece.fbx (same model as Seed_Shield)
- [ ] **Pie_Knight_Sword** — model: Pie_Knight_Sword.fbx, Pie_Knight_Sword_Piece.fbx
- [ ] **Wood_Barrel** — model: Wood_Barrel.fbx — broken: Wood_Barrel_Drop.fbx

### Small breakables & props
- [ ] **Bedroom_Book_1** — model: Bedroom_Book_1.fbx
- [ ] **Bedroom_Book_10** — model: Bedroom_Book_10.fbx
- [ ] **Bedroom_Book_4** — model: Bedroom_Book_4.fbx
- [ ] **Bedroom_Book_5** — model: Bedroom_Book_5.fbx
- [ ] **Bedroom_Book_6** — model: Bedroom_Book_6.fbx
- [ ] **Bedroom_Book_7** — model: Bedroom_Book_7.fbx
- [ ] **Bedroom_Book_8** — model: Bedroom_Book_8.fbx
- [ ] **Bedroom_Book_9** — model: Bedroom_Book_9.fbx

### Pumpkins
- [ ] **Pumpkin_3** (same model as Pumpkin_0, Pumpkin_5)
- [ ] **Pumpkin_4** — model: cow_boy_hat.fbx (same model as Pumpkin_0)
- [ ] **Pumpkin_4_Chaos** (same model as Pumpkin_0, Pumpkin_4)
- [ ] **Pumpkin_7** — model: Witch_Hat_2.fbx (same model as Pumpkin_0)

### Weapons & throwables
- [ ] **Hammer_Tool** — model: Tool_Hammer.fbx — broken: Tool_Hammer_Drop.fbx

### Enemies & bosses
- [ ] **Cockroach_Smarter** — model: Cockroach_Smol_&_Anims.fbx

### Chests, seeds & candy
- [ ] **Candy_1** (same model as Bedroom_Candy_Basket) — broken: Candy_Drop.fbx
- [ ] **Seed_Chest** (same model as Seed_Shield)
- [ ] **Treasure_Chest_and_Seed_Chest** (same model as Treasure_Chest_and_Seed_Shield)

## 5 Playroom (living room, Jenga stage) (20 prefabs, 10 new model files)

### Furniture & big props
- [ ] **Livingroom_pumpkin_music_player** (same model as Bedroom_pumpkin_music_player)
- [ ] **Paint_Bees_Start** — model: Paint_Bees_Meshes.asset
- [ ] **Paint_Bucket** — model: pumpkin_bucket.fbx (same model as Paint_Bees_Start)
- [ ] **Paint_Giant_Slime** — model: Giant_Slime.fbx

### Small breakables & props
- [ ] **Bedroom_Candy_Basket_P** (same model as Bedroom_Candy_Basket, Pumpkin_On_Tree)
- [ ] **Candle_Hang** — model: Candle_Hang.fbx
- [ ] **Jenga_Block** — model: smoke_ball.fbx
- [ ] **Jenga_Block.000** — model: Jenga_Block.fbx
- [ ] **Jenga_Block.001** (same model as Jenga_Block.000)
- [ ] **Jenga_Block.002** (same model as Jenga_Block.000)
- [ ] **Jenga_Block.003** — model: Jenga_Block_Wood.fbx
- [ ] **Jenga_Block.004** (same model as Jenga_Block.003)
- [ ] **Jenga_Block.005** (same model as Jenga_Block.003) — broken: Jenga_Block_Drop.fbx
- [ ] **Paper_Box_0** (same model as Paper_Box_1) — broken: Paper_Box_1_Drop.fbx

### Pumpkins
- [ ] **Pumpkin_6** — model: Skull_Mask.001.fbx (same model as Pumpkin_0)
- [ ] **Pumpkin_6_Chaos** (same model as Pumpkin_0, Pumpkin_6)

### Enemies & bosses
- [ ] **Cockroach_Boss_PumpkinButt** — model: Skull_Mask.000.fbx
- [ ] **Flying_Slime** — model: Flying_Slime.fbx

### Chests, seeds & candy
- [ ] **Seed_Boot** (same model as Seed_Shield)
- [ ] **Treasure_Chest_and_Seed_Boot** (same model as Treasure_Chest_and_Seed_Shield)

## 6 Rooftop again: whack pumpkins, boss, ending (32 prefabs, 15 new model files)

### Furniture & big props
- [ ] **Livingroom_pumpkin_music_player_JACK_** (same model as Bedroom_pumpkin_music_player)
- [ ] **Rooftop_Pole_Light** — model: Rooftop_Pole_Light.fbx — broken: Rooftop_Pole_Light_Drop.fbx
- [ ] **Thank_You_GAME_OVER** — model: Thank_You_GAME_OVER.fbx
- [ ] **Wood_Barrel_1** — model: Wood_Barrel_Jack.fbx
- [ ] **Wood_Barrel_2** (same model as Wood_Barrel_1)
- [ ] **Wood_Barrel_3** (same model as Wood_Barrel_1) — broken: Wood_Barrel_Drop.fbx
- [ ] **Wood_Barrel_4** (same model as Wood_Barrel_1)
- [ ] **Wood_Barrel_5** (same model as Wood_Barrel_1)
- [ ] **Wood_Barrel_6** (same model as Wood_Barrel_1)
- [ ] **Wood_Barrel_7** (same model as Wood_Barrel_1)
- [ ] **Wood_Barrel_8** (same model as Wood_Barrel_1)

### Small breakables & props
- [ ] **Skull_And_Candle** — model: Skull_drop.fbx, Skull_and_Candle.fbx
- [ ] **Web_Ball_On_Barrel** (same model as Spider_Web)

### Weapons & throwables
- [ ] **Boong_Gun** — model: Boong_Bone_Gun.fbx (same model as Skull_And_Candle)
- [ ] **Boong_Skull_Bullet** (same model as Skull_2)
- [ ] **Elon_Boong** — model: Elon_Boong.fbx
- [ ] **Elon_Bullet** (same model as Elon_Boong) — broken: glass_drop.fbx
- [ ] **Hammer_Boong** — model: Boong_E031.fbx — broken: Boong_E031_Drop.fbx
- [ ] **Hammer_Boong_Bone** — model: Boong_Bone.fbx — broken: Skull_drop.fbx

### Enemies & bosses
- [ ] **Bee_Boss** (same model as Spider_Boss_PumpkinButt)
- [ ] **Bee_Boss_PumpkinButt** (same model as Cockroach_Boss_PumpkinButt, Spider_Boss_PumpkinButt)
- [ ] **Bee_Smarter** — model: Bee_Smol_&_Anims.fbx
- [ ] **Bug_Pumpkin** (same model as Pumpkin_00) — broken: pumpkin_0_drop.fbx
- [ ] **pumpkin_boss_Z** — model: Pumpkin_Boss_Z_Butt_Collider.asset, Pumpkin_Boss_Z.fbx — broken: pumpkin_0_drop.fbx
- [ ] **Pumpkin_Mob_Pumpkin_6** — model: Pumpkin_Mob.fbx
- [ ] **Pumpkin_Mob_Pumpkin_7** — model: beehive.fbx (same model as Pumpkin_Mob_Pumpkin_6)

### Chests, seeds & candy
- [ ] **Seed_Bracer** (same model as Seed_Shield)
- [ ] **The_Boss_Seed** — model: The_Boss_Seed.fbx
- [ ] **The_Seed** (same model as The_Seed_Start)
- [ ] **Treasure_Chest_and_Seed** (same model as Treasure_Chest_and_Seed_Shield)
- [ ] **Treasure_Chest_and_Seed_CULUA** (same model as Treasure_Chest_and_Seed_Shield)
- [ ] **Treasure_Chest_GAMEOVER** (same model as Treasure_Chest_and_Seed_Shield)

## Player (everywhere) (1 prefabs, 1 new model files)

### Furniture & big props
- [ ] **Live_or_Die_Coin** — model: Live_or_Die_Coin.fbx

## Later: side paths (PlayGround, bowling) (40 prefabs, 54 new model files)

### Rooms & big pieces
- [ ] **Play_Ground_Z** — model: grass_1_big.fbx, grass_1_medium.fbx, Eye_Poster_L.fbx, grass_2_big.fbx, Eye_Poster_M.fbx, Eye_Poster_R.fbx, BooBoo_Hang.fbx, grass_2_medium.fbx, Big_Tree_01_04.fbx, Mouth_Poster.fbx, BooHoo_Poster.fbx, Play_Ground_Z.fbx, Big_Tree_01_03.fbx, Big_Tree_01_01.fbx, chubby_dog.fbx, chubby_cat.fbx (same model as Bedroom_Base_Z)

### Furniture & big props
- [ ] **Big_Tree_01_01** — model: bushes_01.fbx, bushes_02.fbx, Half_Moon.fbx, Cube_Yellow.fbx, grass_1_small.fbx, grass_2_small.fbx, Star_Yellow.fbx (same model as Play_Ground_Z)
- [ ] **Big_Tree_01_03** — model: bushes_03.fbx (same model as Big_Tree_01_01, Play_Ground_Z)
- [ ] **Big_Tree_01_03_1** (same model as Big_Tree_01_01, Play_Ground_Z)
- [ ] **Big_Tree_01_04** (same model as Big_Tree_01_01, Play_Ground_Z)
- [ ] **Bowling_Arena** — model: Bowling_Dirt.asset
- [ ] **Bowling_Ball** — model: pumpkin_bowling.fbx
- [ ] **Bowling_Vines** — model: Vine_Leaf.asset, Vine_4.asset, Vine_1.asset, Vine_5.asset, Vine_2.asset, Vine_6.asset, Vine_0.asset, Vine_3.asset, Vine_7.asset
- [ ] **bushes_01_03** — model: bushes_05.fbx
- [ ] **bushes_01_04** (same model as Big_Tree_01_01)
- [ ] **Cement_Pipe** — model: Cement_Pipe.fbx — broken: Cement_Pipe_Drop.fbx
- [ ] **Dropped_Bone_Fuel** — model: Dropped_Wood_Fuel.fbx, Bone_Fuel.fbx
- [ ] **Dropped_Wood_Fuel** (same model as Dropped_Bone_Fuel)
- [ ] **Fire_Column** — model: Fire_Plinth.asset, Fire_Woods.fbx
- [ ] **Fire_Woods_n_Cauldron** — model: rock_1_small.fbx, Magical_Pot.fbx, rock_2_big.fbx, rock_2_medium.fbx, rock_1_medium.fbx, rock_1_big.fbx (same model as Fire_Column)
- [ ] **PG_Bench** — model: PG_Bench.fbx — broken: PG_Bench_Drop.fbx
- [ ] **PG_Concrete_Block** (same model as Fire_Woods_n_Cauldron)
- [ ] **PG_Concrete_Pillar** (same model as Fire_Woods_n_Cauldron)
- [ ] **PG_Grass_Hideout_1** (same model as Play_Ground_Z)
- [ ] **PG_Grass_Hideout_2** (same model as Play_Ground_Z)
- [ ] **PG_Grass_Hideout_3** (same model as Play_Ground_Z)
- [ ] **PG_pumpkin_music_player** (same model as Bedroom_pumpkin_music_player)
- [ ] **Recipe_Card** — model: Recipe_Card.fbx
- [ ] **Recipe_Hat** — model: Recipe_Hat.fbx
- [ ] **Slime_Pin** (same model as Flying_Slime)
- [ ] **Table_2_small** — model: Table_2_small.fbx — broken: Table_2_small_Drop.fbx
- [ ] **Wood_Barrel2** — model: Wood_Barrel_2.fbx — broken: Wood_Barrel_2_drop.fbx

### Small breakables & props
- [ ] **Giant_Skull** — model: Giant_Skull.fbx — broken: Giant_Skul_Drop.fbx
- [ ] **Skull_Mini** — model: Giant_Skull_Mini.fbx

### Pumpkins
- [ ] **Pumpkin_10** (same model as Pumpkin_0)
- [ ] **Pumpkin_8** (same model as Pumpkin_0)
- [ ] **Pumpkin_9** (same model as Pumpkin_0)

### Enemies & bosses
- [ ] **Slime_Big** (same model as Paint_Giant_Slime)
- [ ] **Slime_Big_Blue** (same model as Paint_Giant_Slime)
- [ ] **Slime_Big_Red** (same model as Paint_Giant_Slime)
- [ ] **Slime_Big_Yellow** (same model as Paint_Giant_Slime)
- [ ] **Slime_Small** (same model as Paint_Giant_Slime)

### Chests, seeds & candy
- [ ] **Seed_Bracer_Cauldron** (same model as Seed_Shield)
- [ ] **Treasure_Chest_and_Seed_Bracer** (same model as Treasure_Chest_and_Seed_Shield)
- [ ] **Treasure_Chest_and_Seed_Bracer_Cauldron** (same model as Treasure_Chest_and_Seed_Shield)

## Broken pieces whose object isn't in the list above
- [ ] Door_Break_Drop (from Door_Break_Pos) — Bedroom_Door_Drop.fbx
- [ ] Picture_Hang_4_Broken (from Picture_4_Pos) — Picture_Hang_4_Broken.fbx
- [ ] Picture_Hang_3_Broken (from Picture_Hang3_Pos_2) — Picture_Hang_3.fbx, Picture_Hang_3_Broken.fbx
- [ ] Picture_Hang_8_Broken (from Picture_Hang_Pos) — Picture_Hang_8.fbx, Picture_Hang_8_Broken.fbx

Totals: 227 prefabs with a model, 220 model files.
