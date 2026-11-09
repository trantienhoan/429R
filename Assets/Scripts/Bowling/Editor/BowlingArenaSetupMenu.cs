using System.Collections.Generic;
using Game.Enemies;
using Game.Locomotion;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Game.Bowling.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bowling > Set Up Bowling Arena (Gate 2, "Strike the Slimes", 2026-11-08). Builds
    /// Prefabs/Bowling/Bowling_Arena from BowlingLayout: a 5 x 5 m dirt island (the only teleport area, with a spot light
    /// over it) in the middle of an X of 4 lanes (20 m long, 2 m wide, dark metal rails, 6 pin slots, hover, aim and tip
    /// points, a counter, and a lit part: deck spot, deck ring, 2 fire columns, sparkles), 12 Fall Edges round it, 8 ball
    /// sprouts with the Bowling Ball Supply, the vines, the rules sign, and the Bowling Stage wired to the slimes, pins,
    /// flyers, divers, showdown slimes, chests and The_Seed. Also Bowling_Rail_Mat and Bowling_Lane_Mat's tiling.
    /// Run Set Up Bowling Look, Set Up Slime Pins and Set Up Giant Slime And Showdown first. Safe to run again (builds the
    /// arena anew; the prefab keeps its file, so GAMESTAGES keeps pointing at it). Never touches the scene.
    /// </summary>
    public static class BowlingArenaSetupMenu
    {
        private const string MenuPath = "Tools/429 Game/Bowling/Set Up Bowling Arena";
        private const string PrefabFolder = "Assets/Prefabs/Bowling";
        private const string MatFolder = "Assets/Materials/Bowling";
        private const string ArenaPath = PrefabFolder + "/Bowling_Arena.prefab";
        private const string BallPath = PrefabFolder + "/Bowling_Ball.prefab";
        private const string SlimePath = PrefabFolder + "/Giant_Flying_Slime.prefab";
        private const string PinPath = PrefabFolder + "/Slime_Pin.prefab";
        private const string FlyerPath = PrefabFolder + "/Slime_Pin_Flyer.prefab";
        private const string FirePath = PrefabFolder + "/Fire_Column.prefab";
        private const string SparklesPath = PrefabFolder + "/Lane_Sparkles.prefab";
        private const string VinesPath = PrefabFolder + "/Bowling_Vines.prefab";
        private const string DirtMeshPath = "Assets/Models/Bowling/Bowling_Dirt.asset";
        private const string DirtMatPath = MatFolder + "/Bowling_Dirt_Mat.mat";
        private static readonly string[] ShowdownPaths =
        {
            PrefabFolder + "/Slime_Showdown_Red.prefab", PrefabFolder + "/Slime_Showdown_Blue.prefab",
            PrefabFolder + "/Slime_Showdown_Green.prefab", PrefabFolder + "/Slime_Showdown_Yellow.prefab",
        };
        private const string DiverPath = "Assets/Prefabs/Enemies/Daruma_Bomber.prefab";
        private const string StartChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_and_Seed_Bracer.prefab";
        private static readonly string[] PhaseChestPaths =
        {
            "Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Chest.prefab",
            "Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Head.prefab",
            "Assets/Prefabs/PlayGround/PG_Wood/PG_Treasure_Chest_and_Seed_Shield.prefab",
        };
        private const string SeedPath = "Assets/Prefabs/Items/Chest/The_Seed.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string IgnitePath = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/thrusterFire_000.ogg";
        private const string MetalPath = "Assets/Materials/PlayGround/PG_Metal_Mat.mat";
        private const string BoardsPath = "Assets/Materials/PlayGround/PG_Wood_Boards_Mat.mat";
        private const string RingTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle06.png";
        private const string NoFogShader = "429/Particles/Unlit No Fog";

        private const string RulesText = "STRIKE THE SLIMES\n<size=80%>3 strikes in a row hurts one</size>";
        private const int MaxLights = 5;
        private const int MaxParticles = 650;
        private const float NavHalf = 20f;   // LiveNavMesh's world box is +-20 m

        [MenuItem(MenuPath)]
        public static void Menu() => Run();

        [MenuItem(MenuPath, true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        /// <summary>Builds the arena prefab and its materials. False (and nothing built) when something it needs is missing.</summary>
        public static bool Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Bowling] Stop play mode first."); return false; }

            var ball = Load<GameObject>(BallPath);
            var slime = Load<GameObject>(SlimePath);
            var pin = Load<GameObject>(PinPath);
            var flyer = Load<GameObject>(FlyerPath);
            var fire = Load<GameObject>(FirePath);
            var sparkles = Load<GameObject>(SparklesPath);
            var vines = Load<GameObject>(VinesPath);
            var dirtMesh = Load<Mesh>(DirtMeshPath);
            var dirtMat = Load<Material>(DirtMatPath);
            var showdown = new GameObject[ShowdownPaths.Length];
            for (int i = 0; i < showdown.Length; i++) showdown[i] = Load<GameObject>(ShowdownPaths[i]);
            var diver = Load<GameObject>(DiverPath);
            var startChest = Load<GameObject>(StartChestPath);
            var phaseChests = new GameObject[PhaseChestPaths.Length];
            for (int i = 0; i < phaseChests.Length; i++) phaseChests[i] = Load<GameObject>(PhaseChestPaths[i]);
            var seed = Load<GameObject>(SeedPath);
            var puff = Load<GameObject>(PuffPath);
            var whoosh = Load<AudioClip>(Sfx + "whoosh.wav");
            var pop = Load<AudioClip>(Sfx + "bubble_pop_1.wav");
            var ignite = Load<AudioClip>(IgnitePath);
            var metal = Load<Material>(MetalPath);
            var boards = Load<Material>(BoardsPath);
            var ringTexture = Load<Texture2D>(RingTexture);

            var missing = new List<string>();
            if (ball == null) missing.Add(BallPath + " (run Set Up Bowling Ball)");
            if (slime == null || slime.GetComponent<GiantFlyingSlime>() == null) missing.Add(SlimePath + " (with Giant Flying Slime; run Set Up Giant Slime And Showdown)");
            if (pin == null || pin.GetComponent<SlimePin>() == null) missing.Add(PinPath + " (with Slime Pin; run Set Up Slime Pins)");
            if (flyer == null || flyer.GetComponent<SlimeBomber>() == null) missing.Add(FlyerPath + " (with Slime Bomber; run Set Up Slime Pins)");
            if (fire == null) missing.Add(FirePath + " (run Set Up Bowling Look)");
            if (sparkles == null) missing.Add(SparklesPath + " (run Set Up Bowling Look)");
            if (vines == null || vines.GetComponent<VineGrow>() == null) missing.Add(VinesPath + " (with Vine Grow; run Set Up Bowling Look)");
            if (dirtMesh == null) missing.Add(DirtMeshPath + " (run Set Up Bowling Look)");
            if (dirtMat == null) missing.Add(DirtMatPath + " (run Set Up Bowling Look)");
            for (int i = 0; i < showdown.Length; i++)
                if (showdown[i] == null || showdown[i].GetComponent<SpiderBrain>() == null) missing.Add(ShowdownPaths[i] + " (with Spider Brain; run Set Up Giant Slime And Showdown)");
            if (diver == null || diver.GetComponent<SlimeBomber>() == null) missing.Add(DiverPath + " (with Slime Bomber)");
            if (startChest == null) missing.Add(StartChestPath);
            for (int i = 0; i < phaseChests.Length; i++) if (phaseChests[i] == null) missing.Add(PhaseChestPaths[i]);
            if (seed == null) missing.Add(SeedPath);
            if (puff == null) missing.Add(PuffPath);
            if (whoosh == null) missing.Add(Sfx + "whoosh.wav");
            if (pop == null) missing.Add(Sfx + "bubble_pop_1.wav");
            if (ignite == null) missing.Add(IgnitePath);
            if (metal == null) missing.Add(MetalPath);
            if (boards == null) missing.Add(BoardsPath);
            if (ringTexture == null) missing.Add(RingTexture);
            if (Shader.Find(NoFogShader) == null) missing.Add("shader " + NoFogShader);
            if (missing.Count > 0)
            {
                Debug.LogError("[Bowling] Set Up Bowling Arena: nothing built, missing:\n" + string.Join("\n", missing));
                return false;
            }

            Folder("Assets", "Prefabs");
            Folder("Assets/Prefabs", "Bowling");
            Folder("Assets", "Materials");
            Folder("Assets/Materials", "Bowling");
            bool vAlongZ = TopFaceVAlongZ();
            var laneMat = LaneMaterial(boards);
            var railMat = RailMaterial(metal);
            var ringMat = RingMaterial();

            var notes = new List<string>();
            // Built in a preview scene: the open scene (or a prefab open in Prefab Mode) is never touched.
            var build = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Bowling_Arena");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, build);
            try
            {
                int teleportLayer = LayerMask.NameToLayer("Teleport");
                if (teleportLayer < 0) teleportLayer = 31;
                var x = new GameObject("X").transform;
                x.SetParent(root.transform, false);
                x.localRotation = Quaternion.Euler(0f, 45f, 0f);

                var centre = new GameObject("Centre").transform;
                centre.SetParent(x, false);

                // The dirt island: the only teleport area.
                var dirt = MakeDirt(x, dirtMesh, dirtMat, teleportLayer);
                Spot(x, "Dirt Light", new Vector3(0f, BowlingLayout.DirtLightHeight, 0f), 60f, 40f, 9f, 5f, new Color(1f, 0.78f, 0.5f));

                // The lanes (lane k turned -90k degrees: 0 along +Z, 1 along -X, 2 along -Z, 3 along +X).
                var lanes = new BowlingLane[4];
                for (int k = 0; k < 4; k++)
                    lanes[k] = MakeLane(x, k, teleportLayer, vAlongZ, laneMat, railMat, ringMat, fire, sparkles, whoosh, ignite, puff);

                // Fall Edges: 2 per quadrant beside the lanes (quadrant 0 between lane 0 and lane 3, the rest turned by
                // -90q), 1 past each tip; never over the dirt or a lane.
                var edges = new GameObject("Fall Edges").transform;
                edges.SetParent(x, false);
                int edgeCount = 0;
                for (int q = 0; q < 4; q++)
                {
                    var turn = Quaternion.Euler(0f, -90f * q, 0f);
                    bool swap = q % 2 == 1;
                    var sizeA = new Vector3(41.6f, 5f, 40.1f);
                    var sizeB = new Vector3(40.1f, 5f, 1.5f);
                    Edge(edges, "Fall Edge A" + q, turn * new Vector3(21.8f, 1.5f, 22.55f), swap ? Swap(sizeA) : sizeA);
                    Edge(edges, "Fall Edge B" + q, turn * new Vector3(22.55f, 1.5f, 1.75f), swap ? Swap(sizeB) : sizeB);
                    edgeCount += 2;
                }
                for (int k = 0; k < 4; k++)
                {
                    var d = BowlingLayout.LaneDir(k);
                    float length = 20f;
                    float mid = BowlingLayout.LaneEnd + length * 0.5f;
                    var size = Mathf.Abs(d.z) > 0.5f ? new Vector3(BowlingLayout.LaneWidth, 5f, length) : new Vector3(length, 5f, BowlingLayout.LaneWidth);
                    Edge(edges, "Fall Edge Tip " + k, BowlingLayout.LanePoint(k, mid, 0f, 1.5f), size);
                    edgeCount++;
                }

                // Ball sprouts round the centre (the vine tips), with the supply (aligned with X: its pad square).
                var sprouts = new GameObject("Ball Sprouts");
                sprouts.transform.SetParent(x, false);
                var spots = new List<Object>();
                for (int i = 0; i < BowlingLayout.SproutYaws.Length; i++)
                    spots.Add(Place(sprouts.transform, "Spot " + i, BowlingLayout.SproutPoint(i)));
                var supply = sprouts.AddComponent<BowlingBallSupply>();
                var sup = new SerializedObject(supply);
                Set(sup, "ballPrefab", ball);
                SetArray(sup, "spots", spots.ToArray());
                SetInt(sup, "maxOnPad", 2);
                SetInt(sup, "openSpots", 4);
                SetInt(sup, "maxAlive", 9);
                SetFloat(sup, "growSeconds", 3f);
                SetFloat(sup, "regrowDelay", 1f);
                SetFloat(sup, "padHalfSize", 2.6f);
                SetFloat(sup, "stallSeconds", 8f);
                SetFloat(sup, "keepFromHead", 0.6f);
                Set(sup, "puff", puff);
                SetFloat(sup, "puffScale", 0.5f);
                Set(sup, "growSound", whoosh);
                Set(sup, "popSound", pop);
                SetFloat(sup, "volume", 0.7f);
                sup.ApplyModifiedPropertiesWithoutUndo();

                // The vines (their tips are the sprouts).
                var vineGo = (GameObject)PrefabUtility.InstantiatePrefab(vines, x);
                vineGo.name = "Vines";
                vineGo.transform.localPosition = Vector3.zero;
                vineGo.transform.localRotation = Quaternion.identity;
                foreach (var c in vineGo.GetComponentsInChildren<Collider>(true)) c.enabled = false;

                // The rules sign over lane 0's mouth, facing the centre (the stage shows it).
                var rules = Text(x, "Rules Sign", new Vector3(0f, BowlingLayout.RulesHeight, BowlingLayout.RulesAt), Vector3.zero, RulesText, 2f, new Vector2(4f, 1.2f));
                rules.gameObject.SetActive(false);

                var pins = new GameObject("Pins").transform;
                pins.SetParent(root.transform, false);

                var stage = root.AddComponent<BowlingStage>();
                var so = new SerializedObject(stage);
                SetArray(so, "lanes", lanes);
                Set(so, "centre", centre);
                SetFloat(so, "padHalfSize", BowlingLayout.DirtHalf);
                Set(so, "balls", supply);
                Set(so, "vines", vineGo.GetComponent<VineGrow>());
                Set(so, "rulesSign", rules);
                Set(so, "pinsRoot", pins);
                Set(so, "slimePrefab", slime.GetComponent<GiantFlyingSlime>());
                Set(so, "pinPrefab", pin.GetComponent<SlimePin>());
                Set(so, "flyerPrefab", flyer);
                Set(so, "diverPrefab", diver);
                SetArray(so, "showdownPrefabs", showdown);
                Set(so, "seedPrefab", seed);
                Set(so, "puff", puff);
                Set(so, "startChest", startChest);
                SetArray(so, "phaseChests", phaseChests);
                var fog = Prop(so, "fog");
                if (fog != null) fog.vector2Value = new Vector2(14f, 38f);
                SetString(so, "rulesText", RulesText);
                so.ApplyModifiedPropertiesWithoutUndo();

                // Quest: no shadows anywhere.
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                int lights = 0, shadowed = 0, alwaysOn = 0;
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    l.shadows = LightShadows.None;
                    if (l.shadows != LightShadows.None) shadowed++;
                    lights++;
                    if (!UnderLit(l.transform)) alwaysOn++;
                }
                int particles = 0;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) particles += ps.main.maxParticles;
                float reach = NavReach(root.transform, dirt, lanes);

                PrefabUtility.SaveAsPrefabAsset(root, ArenaPath, out bool saved);
                if (!saved)
                {
                    Debug.LogError("[Bowling] Could not save " + ArenaPath + ".");
                    return false;
                }
                notes.Add("Bowling_Arena: dirt " + BowlingLayout.DirtHalf * 2f + " x " + BowlingLayout.DirtHalf * 2f + " m (teleport area), 4 lanes " +
                          (BowlingLayout.LaneEnd - BowlingLayout.LaneStart) + " x " + BowlingLayout.LaneWidth + " m, 6 slots each, " +
                          edgeCount + " Fall Edges, " + BowlingLayout.SproutYaws.Length + " ball sprouts");
                notes.Add("lights " + lights + " (max " + MaxLights + ", " + alwaysOn + " always on), shadows " + shadowed);
                notes.Add("particle max sum " + particles + " (limit " + MaxParticles + ")");
                notes.Add("LiveNavMesh check: floor corners reach |x|,|z| " + reach.ToString("0.00") + " m (limit " + NavHalf + ")");
                notes.Add("lane boards: cube top-face v runs along " + (vAlongZ ? "Z (floor as is)" : "X (floor turned 90)") + ", tiling (1, 10.1)");
                notes.Add("slime " + slime.name + ", pin " + pin.name + ", flyer " + flyer.name + ", diver " + diver.name + ", seed " + seed.name);
                if (lights > MaxLights) Debug.LogWarning("[Bowling] " + lights + " lights in the arena (Quest budget " + MaxLights + ").");
                if (particles > MaxParticles) Debug.LogWarning("[Bowling] Particle max sum " + particles + " is over " + MaxParticles + ".");
                if (reach > NavHalf) Debug.LogWarning("[Bowling] A floor corner is " + reach.ToString("0.00") + " m out: past LiveNavMesh's +-" + NavHalf + " m box.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(build);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Bowling] Set Up Bowling Arena:\n" + string.Join("\n", notes));
            return true;
        }

        // The dirt island: the mesh, a box collider under its top, the teleport area (bit 31).
        private static GameObject MakeDirt(Transform x, Mesh mesh, Material material, int teleportLayer)
        {
            var dirt = new GameObject("Dirt");
            dirt.transform.SetParent(x, false);
            dirt.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = dirt.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var box = dirt.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.13f, 0f);
            box.size = new Vector3(BowlingLayout.DirtHalf * 2f, 0.3f, BowlingLayout.DirtHalf * 2f);
            SetLayer(dirt, teleportLayer);
            var area = dirt.AddComponent<TeleportationArea>();
            // Bit 31: the scene's Teleport Interactor and every other teleport area use it. Not GetMask("Teleport"):
            // indexes 1 and 31 are both named Teleport and it returns 1, which that ray never selects.
            area.interactionLayers = unchecked((int)0x80000000);
            area.colliders.Clear();
            area.colliders.Add(box);
            area.matchOrientation = MatchOrientation.None;
            return dirt;
        }

        // One lane, turned to point its way from the centre: floor, rails, the lit part (deck spot, ring, fire columns,
        // sparkles), the slots, aim, hover, tip and counter.
        private static BowlingLane MakeLane(Transform x, int k, int teleportLayer, bool vAlongZ, Material laneMat, Material railMat,
                                            Material ringMat, GameObject fire, GameObject sparkles, AudioClip whoosh, AudioClip ignite, GameObject puff)
        {
            var lane = new GameObject("Lane " + k).transform;
            lane.SetParent(x, false);
            lane.localRotation = Quaternion.Euler(0f, -90f * k, 0f);
            float length = BowlingLayout.LaneEnd - BowlingLayout.LaneStart;
            float mid = (BowlingLayout.LaneStart + BowlingLayout.LaneEnd) * 0.5f;

            // The floor (teleport layer for the NavMesh and the ray, but no teleport area: the ray shows invalid).
            GameObject floor;
            if (vAlongZ) floor = Box(lane, "Floor", new Vector3(0f, -0.15f, mid), new Vector3(BowlingLayout.LaneWidth, 0.3f, length), laneMat);
            else
            {
                // The cube's top face runs its v across X: turned so the boards run along the lane.
                floor = Box(lane, "Floor", new Vector3(0f, -0.15f, mid), new Vector3(length, 0.3f, BowlingLayout.LaneWidth), laneMat);
                floor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            SetLayer(floor, teleportLayer);
            // Rails from the foul line to the tip (open at the tip), Default layer, they keep the ball in.
            float railLength = BowlingLayout.LaneEnd - BowlingLayout.FoulLine;
            float railMid = (BowlingLayout.FoulLine + BowlingLayout.LaneEnd) * 0.5f;
            Box(lane, "Rail L", new Vector3(-BowlingLayout.RailSide, 0.1f, railMid), new Vector3(0.1f, 0.2f, railLength), railMat);
            Box(lane, "Rail R", new Vector3(BowlingLayout.RailSide, 0.1f, railMid), new Vector3(0.1f, 0.2f, railLength), railMat);

            var lit = new GameObject("Lit");
            lit.transform.SetParent(lane, false);
            Flat(lit.transform, "Deck Ring", new Vector3(0f, 0.01f, BowlingLayout.DeckAt), ringMat, BowlingLayout.RingSize);
            var deckLight = Spot(lit.transform, "Deck Light", new Vector3(0f, BowlingLayout.DeckLightHeight, BowlingLayout.DeckAt), 45f, 25f, 10f, 6f, new Color(1f, 0.65f, 0.35f));
            var fires = new List<Object>();
            foreach (float side in new[] { -BowlingLayout.FireSide, BowlingLayout.FireSide })
            {
                var column = (GameObject)PrefabUtility.InstantiatePrefab(fire, lit.transform);
                column.name = side < 0f ? "Fire Column L" : "Fire Column R";
                column.transform.localPosition = new Vector3(side, 0f, BowlingLayout.FireAt);
                column.transform.localRotation = Quaternion.identity;
                // Over the void: looks only.
                foreach (var c in column.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                fires.Add(column.transform);
            }
            var twinkle = (GameObject)PrefabUtility.InstantiatePrefab(sparkles, lit.transform);
            twinkle.name = "Sparkles";
            twinkle.transform.localPosition = Vector3.zero;
            twinkle.transform.localRotation = Quaternion.identity;
            // Dark until a slime picks it (the stage lights it): the fire ignites when this comes on.
            lit.SetActive(false);

            var slots = new Object[BowlingLayout.Rack.Length];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Place(lane, "Slot " + i, new Vector3(BowlingLayout.Rack[i].x, 0f, BowlingLayout.Rack[i].y));
            var aim = Place(lane, "Aim", new Vector3(0f, BowlingLayout.AimHeight, BowlingLayout.HeadPin));
            var hover = Place(lane, "Hover", new Vector3(0f, 3.3f, BowlingLayout.DeckAt));
            var tip = Place(lane, "Tip", new Vector3(0f, 0f, BowlingLayout.LaneEnd));
            var counter = Text(lane, "Counter", new Vector3(0f, 0.05f, BowlingLayout.CounterAt), new Vector3(70f, 0f, 0f), "0/3", 1.2f, new Vector2(1.6f, 0.4f));
            counter.gameObject.SetActive(false);

            var component = lane.gameObject.AddComponent<BowlingLane>();
            var so = new SerializedObject(component);
            SetInt(so, "index", k);
            Set(so, "aim", aim);
            Set(so, "tip", tip);
            Set(so, "hover", hover);
            SetArray(so, "slots", slots);
            SetFloat(so, "mouthDistance", BowlingLayout.FoulLine);
            Set(so, "litRoot", lit);
            SetArray(so, "reveal", System.Array.Empty<Object>());
            SetArray(so, "strips", System.Array.Empty<Object>());
            Set(so, "deckLight", deckLight);
            Set(so, "counter", counter);
            SetArray(so, "fires", fires.ToArray());
            Set(so, "revealSound", whoosh);
            SetFloat(so, "revealVolume", 0.7f);
            Set(so, "igniteSound", ignite);
            SetFloat(so, "igniteVolume", 0.7f);
            SetFloat(so, "igniteDistance", 4f);
            Set(so, "puff", puff);
            SetFloat(so, "puffScale", 0.6f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static Vector3 Swap(Vector3 size) => new(size.z, size.y, size.x);

        // Under one of the lanes' "Lit" roots (on only while the lane is lit).
        private static bool UnderLit(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "Lit") return true;
            return false;
        }

        // The farthest |x| or |z| (arena = world frame, at the origin) of the dirt's and lanes' floor corners.
        private static float NavReach(Transform root, GameObject dirt, BowlingLane[] lanes)
        {
            float reach = 0f;
            var floors = new List<Transform> { dirt.transform };
            foreach (var lane in lanes)
            {
                var floor = lane != null ? lane.transform.Find("Floor") : null;
                if (floor != null) floors.Add(floor);
            }
            foreach (var floor in floors)
            {
                var box = floor.GetComponent<BoxCollider>();
                if (box == null) continue;
                foreach (float sx in new[] { -0.5f, 0.5f })
                {
                    foreach (float sz in new[] { -0.5f, 0.5f })
                    {
                        var local = box.center + Vector3.Scale(box.size, new Vector3(sx, 0f, sz));
                        var p = root.InverseTransformPoint(floor.TransformPoint(local));
                        reach = Mathf.Max(reach, Mathf.Abs(p.x), Mathf.Abs(p.z));
                    }
                }
            }
            return reach;
        }

        // ------------------------------------------------------------------ materials

        // The PlayGround's purple boards, darker, tiled so a 2 x 20.2 m lane shows boards 2 m to a repeat.
        private static Material LaneMaterial(Material boards)
        {
            string path = MatFolder + "/Bowling_Lane_Mat.mat";
            if (Load<Material>(path) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(boards), path);
            var m = Load<Material>(path);
            var tiling = new Vector2(1f, 10.1f);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
            if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", tiling);
            if (m.HasProperty("_BaseColor"))
            {
                float alpha = m.GetColor("_BaseColor").a;
                m.SetColor("_BaseColor", new Color(0.75f, 0.75f, 0.75f, alpha));
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // PG_Metal without its glow (it would light the whole dark lane faintly).
        private static Material RailMaterial(Material metal)
        {
            string path = MatFolder + "/Bowling_Rail_Mat.mat";
            if (Load<Material>(path) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(metal), path);
            var m = Load<Material>(path);
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }

        // The deck ring: Circle06, additive, free of the fog (as Paint_Bees_Ring_Mat).
        private static Material RingMaterial()
        {
            string path = MatFolder + "/Bowling_Deck_Ring_Mat.mat";
            var m = Load<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(NoFogShader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(NoFogShader);
            m.SetTexture("_BaseMap", Load<Texture2D>(RingTexture));
            m.SetColor("_BaseColor", new Color(1f, 0.6f, 0.2f, 0.8f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Which way the built-in cube's top face runs its v (the boards' length in pg_wood_boards): along Z or X.
        private static bool TopFaceVAlongZ()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices;
                var n = mesh.normals;
                var uv = mesh.uv;
                float alongZ = 0f, alongX = 0f;
                for (int i = 0; i < v.Length; i++)
                {
                    if (n[i].y < 0.9f) continue;
                    for (int j = i + 1; j < v.Length; j++)
                    {
                        if (n[j].y < 0.9f) continue;
                        var d = v[j] - v[i];
                        float dv = Mathf.Abs(uv[j].y - uv[i].y);
                        if (Mathf.Abs(d.x) < 0.0001f && Mathf.Abs(d.z) > 0.5f) alongZ += dv;
                        else if (Mathf.Abs(d.z) < 0.0001f && Mathf.Abs(d.x) > 0.5f) alongX += dv;
                    }
                }
                return alongZ >= alongX;
            }
            finally { Object.DestroyImmediate(probe); }
        }

        // ------------------------------------------------------------------ building helpers (as Daruma Stage Setup)

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        private static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        private static Transform Place(Transform parent, string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        private static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material, bool collide = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Edge(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<FallEdge>();   // before sizing: its Reset sizes a fresh box
            var box = go.GetComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
            box.isTrigger = true;
            go.layer = 2;
        }

        // A spot light pointing straight down, no shadows.
        private static Light Spot(Transform parent, string name, Vector3 at, float angle, float inner, float range, float intensity, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.innerSpotAngle = inner;
            l.color = colour;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        private static TMP_Text Text(Transform parent, string name, Vector3 at, Vector3 euler, string text, float size, Vector2 box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localEulerAngles = euler;
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = box;
            // No outline: setting one here makes a material copy in edit mode that the prefab can't keep.
            t.color = Color.white;
            return t;
        }

        // A flat quad lying on the floor (facing up), this many metres across, with no collider.
        private static void Flat(Transform parent, string name, Vector3 at, Material material, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = at;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one * size;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ serialized fields (a missing one is an error)

        private static SerializedProperty Prop(SerializedObject so, string name)
        {
            var p = so.FindProperty(name);
            if (p == null) Debug.LogError("[Bowling] " + so.targetObject.GetType().Name + " has no field '" + name + "'.");
            return p;
        }

        private static void Set(SerializedObject so, string name, Object value)
        {
            var p = Prop(so, name);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = Prop(so, name);
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var p = Prop(so, name);
            if (p != null) p.intValue = value;
        }

        private static void SetString(SerializedObject so, string name, string value)
        {
            var p = Prop(so, name);
            if (p != null) p.stringValue = value;
        }

        private static void SetArray(SerializedObject so, string name, Object[] values)
        {
            var p = Prop(so, name);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
