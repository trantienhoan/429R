using System.Collections.Generic;
using System.IO;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Game.EditorTools.DangerFlameSetupMenu;
using static UnityEngine.ParticleSystem;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Fairy > Fairy Lock Tower (user, 2026-11-11: remove the bone cage locking the Fairy and make it
    /// "a locking purple tower particle, circling around the fairy", gone when the start pumpkin drops, like the cage).
    /// 1. Builds Assets/Prefabs/Effects/Fairy_Lock_Tower: a purple seal turning on the floor, 8 thin light bars circling
    ///    the Fairy 0.85 m out and 1.4 m tall, a ring of light on top of them and a few sparks spiralling up (about 30
    ///    particles in 4 systems, additive and unlit, no light), the release burst (a flash, a ring racing out along the
    ///    floor, 16 sparks, the barrel seals' glass-break sound in place of the old bones' wood break) and Fairy Lock
    ///    Tower, the script that plays them. Materials: Assets/Data/Effects/Fairy Lock *.mat (the barrel seals' seal and
    ///    ring textures, Lana's spark01 and Circle01).
    /// 2. Changes Assets/Prefabs/Player/Fairy.prefab (copied to Documents/429R_claude_backups/2026-11-11/fairy_lock/
    ///    first; a later run keeps that first copy):
    ///    - The_Cage loses its bone mesh (Mesh Filter + Mesh Renderer). Its trigger box, kinematic body and Start FSM stay:
    ///      they are the lock (the start pumpkin's Monster collider or the Fairy Shield touching it destroys it);
    ///    - The_Cage_Drop_Pos loses its FSM, which spawned the broken bones (The_Cage_Drop); the empty object stays, so
    ///      the cage FSM's "Set Stage 2" still has something to switch on;
    ///    - the three Skull_Candle_Purple under the cage (already off and Editor Only) go;
    ///    - Fairy_Lock_Tower goes next to The_Cage, watching it.
    /// The scene needs nothing: GAMESTAGES' Fairy_Pos spawns the Fairy from the prefab. Safe to run again: the tower is
    /// rebuilt in place and the Fairy only changes where something is still to do. Stop Play mode first.
    /// </summary>
    public static class FairyLockTowerMenu
    {
        private const string MenuPath = "Tools/429 Game/Fairy/Fairy Lock Tower";
        private const string TowerPath = "Assets/Prefabs/Effects/Fairy_Lock_Tower.prefab";
        private const string FairyPath = "Assets/Prefabs/Player/Fairy.prefab";
        private const string TowerName = "Fairy_Lock_Tower";
        private const string CageName = "The_Cage";
        private const string DropName = "The_Cage_Drop_Pos";
        private const string CandleName = "Skull_Candle_Purple";
        private const string BackupArea = "fairy_lock";

        private const string SealTexture = "Assets/Data/Effects/Textures/Seal_Circle.png";
        private const string RingTexture = "Assets/Data/Effects/Textures/Seal_Ring.png";
        private const string BarTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/spark01.png";
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string BreakSound = "Assets/Audio/SFX/glass_break_2.wav";

        // The Fairy floats 7 cm above the floor at GAMESTAGES' Fairy_Pos: the tower's foot goes 1 cm above the floor.
        private const float TowerLocalY = -0.06f;
        // Round the Fairy like the old cage (1.77 x 1.71 m, 1 m tall), but taller: a tower.
        private const float Radius = 0.85f;
        // The bars show from the floor to 1.4 m. spark01's pill fills about 72% of the picture's length, so each bar is
        // drawn longer than that, its soft ends running into the floor and up to the top ring.
        private const float TowerHeight = 1.4f;
        private const float BarLength = TowerHeight / 0.72f;
        private const float BarWidth = 0.14f;
        private const short BarCount = 8;
        // Where the Fairy's middle is, above the tower's foot.
        private const float FairyMiddle = 0.25f;
        // The ring in Seal_Ring is at 0.88 of the texture's half-width, the seal's outer ring at 0.965.
        private const float RingTextureRadius = 0.88f;
        private const float SealTextureRadius = 0.965f;
        // Particles that last until the script fades them (it tells them apart by this).
        private const float LivesForGood = 100000f;

        private static readonly Color Lilac = new(0.80f, 0.55f, 1f);
        private static readonly Color Purple = new(0.58f, 0.28f, 1f);
        private static readonly Color Pink = new(0.95f, 0.50f, 1f);

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Fairy Lock Tower] Stop play mode first. Nothing changed."); return; }
            var tower = BuildTower();
            if (tower == null) return;
            var notes = new List<string> { "built " + TowerPath, FixFairy(tower),
                "the scene needs nothing (GAMESTAGES' Fairy_Pos spawns the Fairy from the prefab)" };
            AssetDatabase.SaveAssets();
            Debug.Log("[Fairy Lock Tower] " + string.Join("; ", notes));
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun() => !EditorApplication.isPlaying;

        // ---- the tower prefab ----

        private static GameObject BuildTower()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == TowerPath)
            {
                Debug.LogError("[Fairy Lock Tower] " + TowerName + " is open in Prefab Mode: close it and run again. Nothing changed.");
                return null;
            }
            var sealMat = MakeMaterial("Fairy Lock Seal", SealTexture, true);
            var ringMat = MakeMaterial("Fairy Lock Ring", RingTexture, true);
            var barMat = MakeMaterial("Fairy Lock Bar", BarTexture, true);
            var glowMat = MakeMaterial("Fairy Lock Glow", GlowTexture, true);

            // Rebuilt in place: the root and its script keep their ids, so the Fairy's copy keeps its settings.
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(TowerPath) : new GameObject(TowerName);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                var script = root.GetComponent<FairyLockTower>();
                if (script == null) script = root.AddComponent<FairyLockTower>();

                var towerGroup = Child(root, "Tower");
                Seal(Child(towerGroup, "Seal"), sealMat);
                var bars = Bars(Child(towerGroup, "Bars"), barMat);
                var top = TopRing(Child(towerGroup, "Top Ring"), ringMat);
                Sparks(Child(towerGroup, "Sparks"), glowMat);

                var releaseGroup = Child(root, "Release");
                ReleaseFlash(Child(releaseGroup, "Release Flash"), glowMat);
                ReleaseRing(Child(releaseGroup, "Release Ring"), ringMat);
                ReleaseSparks(Child(releaseGroup, "Release Sparks"), glowMat);
                var sound = ReleaseSound(root);

                var so = new SerializedObject(script);
                so.FindProperty("releaseSound").objectReferenceValue = sound;
                so.FindProperty("tower").objectReferenceValue = towerGroup.transform;
                so.FindProperty("release").objectReferenceValue = releaseGroup.transform;
                so.FindProperty("turning").objectReferenceValue = bars.transform;
                var lifted = so.FindProperty("liftedParts");
                lifted.arraySize = 2;
                lifted.GetArrayElementAtIndex(0).objectReferenceValue = bars;
                lifted.GetArrayElementAtIndex(1).objectReferenceValue = top;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, TowerPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        // The seal on the floor, as wide as the tower, turning slowly. When the lock breaks it grows half as big again
        // as it fades.
        private static void Seal(GameObject go, Material material)
        {
            go.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            var ps = WaitingSystem(go, material, ParticleSystemRenderMode.HorizontalBillboard, 1);
            ForGood(ps, 1);
            var main = ps.main;
            main.startSize = 2f * (Radius + 0.07f) / SealTextureRadius;
            main.startColor = new Color(Purple.r, Purple.g, Purple.b, 0.85f);
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = 0.25f;
            FadeWhenReleased(ps, 1.5f);
        }

        // Thin bars of light standing round the Fairy, evenly spread. The script turns this object, so they circle her.
        private static ParticleSystem Bars(GameObject go, Material material)
        {
            var ps = WaitingSystem(go, material, ParticleSystemRenderMode.VerticalBillboard, BarCount);
            ForGood(ps, BarCount);
            var main = ps.main;
            // spark01 is a pill lying on its side: the start rotation turns the picture upright inside the particle,
            // which is then sized thin and tall.
            main.startSize3D = true;
            main.startSizeX = BarWidth;
            main.startSizeY = BarLength;
            main.startSizeZ = 1f;
            main.startRotation = Mathf.PI * 0.5f;
            main.startColor = new Color(Lilac.r, Lilac.g, Lilac.b, 0.9f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Radius;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.position = new Vector3(0f, 0.01f + TowerHeight * 0.5f, 0f);
            FadeWhenReleased(ps, 1f);
            return ps;
        }

        // A ring of light round the tops of the bars.
        private static ParticleSystem TopRing(GameObject go, Material material)
        {
            go.transform.localPosition = new Vector3(0f, TowerHeight - 0.02f, 0f);
            var ps = WaitingSystem(go, material, ParticleSystemRenderMode.HorizontalBillboard, 1);
            ForGood(ps, 1);
            var main = ps.main;
            main.startSize = 2f * Radius / RingTextureRadius;
            main.startColor = new Color(Purple.r, Purple.g, Purple.b, 0.6f);
            FadeWhenReleased(ps, 1f);
            return ps;
        }

        // A few sparks spiralling up round the bars (about 15 at a time).
        private static void Sparks(GameObject go, Material material)
        {
            var ps = WaitingSystem(go, material, ParticleSystemRenderMode.Billboard, 20);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(2f, 2.6f);
            main.startSize = new MinMaxCurve(0.05f, 0.09f);
            main.startColor = new MinMaxGradient(Lilac, Pink);
            var emission = ps.emission;
            emission.rateOverTime = 6f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Radius - 0.05f;
            shape.radiusThickness = 0.15f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.position = new Vector3(0f, 0.05f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new MinMaxCurve(0f, 0f);
            velocity.y = new MinMaxCurve(0.5f, 0.75f);
            velocity.z = new MinMaxCurve(0f, 0f);
            velocity.orbitalX = new MinMaxCurve(0f, 0f);
            // Radians a second, the same way round as the bars (a little faster).
            velocity.orbitalY = new MinMaxCurve(0.5f, 0.65f);
            velocity.orbitalZ = new MinMaxCurve(0f, 0f);
            Fade(ps, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            Shrink(ps, 1f, 1f, 0.4f);
        }

        // ---- the release burst ----

        // A soft flash of light round the Fairy.
        private static void ReleaseFlash(GameObject go, Material material)
        {
            go.transform.localPosition = new Vector3(0f, FairyMiddle, 0f);
            var ps = WaitingSystem(go, material, ParticleSystemRenderMode.Billboard, 1);
            Once(ps, 1, 0.35f, 1.6f);
            Fade(ps, new[] { Key(1f, 0.9f, 1f, 0f), Key(Purple.r, Purple.g, Purple.b, 1f) },
                 new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            Grow(ps, 0.5f, 1.2f);
        }

        // A ring racing out along the floor from the foot of the tower.
        private static void ReleaseRing(GameObject go, Material material)
        {
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var ps = WaitingSystem(go, material, ParticleSystemRenderMode.HorizontalBillboard, 1);
            Once(ps, 1, 0.6f, 2f * Radius / RingTextureRadius);
            Fade(ps, new[] { Key(1f, 0.85f, 1f, 0f), Key(Purple.r, Purple.g, Purple.b, 1f) },
                 new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            Grow(ps, 1f, 2.6f);
        }

        // Sparks bursting up and out from the Fairy.
        private static void ReleaseSparks(GameObject go, Material material)
        {
            go.transform.localPosition = new Vector3(0f, FairyMiddle, 0f);
            var ps = WaitingSystem(go, material, ParticleSystemRenderMode.Billboard, 20);
            Once(ps, 16, 0.9f, 0.08f);
            var main = ps.main;
            main.startLifetime = new MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new MinMaxCurve(1.5f, 3f);
            main.startSize = new MinMaxCurve(0.05f, 0.09f);
            main.startColor = new MinMaxGradient(Lilac, Pink);
            main.gravityModifier = 0.3f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Fade(ps, new[] { Key(1f, 1f, 1f, 0f), Key(Purple.r, Purple.g, Purple.b, 1f) },
                 new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        }

        // The seal-breaking sound (the barrel seals' glass break), heard round the Fairy like the old cage breaking.
        private static AudioSource ReleaseSound(GameObject root)
        {
            var sound = root.GetComponent<AudioSource>();
            if (sound == null) sound = root.AddComponent<AudioSource>();
            sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BreakSound);
            sound.playOnAwake = false;
            sound.loop = false;
            sound.volume = 0.8f;
            sound.spatialBlend = 1f;
            sound.rolloffMode = AudioRolloffMode.Logarithmic;
            sound.minDistance = 3f;
            sound.maxDistance = 25f;
            sound.dopplerLevel = 0f;
            return sound;
        }

        // ---- particle helpers ----

        // A system that waits for the script: never plays by itself.
        private static ParticleSystem WaitingSystem(GameObject go, Material material, ParticleSystemRenderMode mode, int max)
        {
            var ps = NewSystem(go, material, mode, max);
            var main = ps.main;
            main.playOnAwake = false;
            return ps;
        }

        // Count particles at once that last until the lock breaks (the script then fades them).
        private static void ForGood(ParticleSystem ps, short count)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = 1f;
            main.startLifetime = LivesForGood;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, count) });
            var shape = ps.shape;
            shape.enabled = false;
        }

        // Played once when the lock breaks: Count particles at once, living Life seconds, Size big.
        private static void Once(ParticleSystem ps, short count, float life, float size)
        {
            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = 1f;
            main.startLifetime = life;
            main.startSize = size;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new Burst(0f, count) });
            var shape = ps.shape;
            shape.enabled = false;
        }

        // A part that lasts until the lock breaks shows its colour and size over lifetime only then, when the script
        // gives it a short life: it fades out, growing to Grow times its size.
        private static void FadeWhenReleased(ParticleSystem ps, float grow)
        {
            Fade(ps, new[] { Key(1f, 1f, 1f, 0f), Key(1f, 1f, 1f, 1f) },
                 new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            if (grow > 1f) Grow(ps, 1f, grow);
        }

        // Size over its life: From to To times its start size, fast at first, then slower.
        private static void Grow(ParticleSystem ps, float from, float to)
        {
            float top = Mathf.Max(from, to);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new MinMaxCurve(top, new AnimationCurve(
                new Keyframe(0f, from / top, 0f, 2f * (to - from) / top), new Keyframe(1f, to / top, 0f, 0f)));
        }

        // ---- the Fairy ----

        private static string FixFairy(GameObject towerPrefab)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == FairyPath) return "Fairy is open in Prefab Mode: close it and run again (Fairy unchanged)";
            var root = PrefabUtility.LoadPrefabContents(FairyPath);
            try
            {
                var cage = root.transform.Find(CageName);
                if (cage == null) return "Fairy has no " + CageName + " (Fairy unchanged)";

                // What is still to do.
                var mesh = cage.GetComponent<MeshRenderer>();
                var filter = cage.GetComponent<MeshFilter>();
                var drop = cage.Find(DropName);
                var dropFsm = drop != null ? drop.GetComponent<PlayMakerFSM>() : null;
                var candles = new List<GameObject>();
                foreach (Transform child in cage)
                    if (child.name == CandleName) candles.Add(child.gameObject);
                var placed = root.transform.Find(TowerName);
                var script = placed != null ? placed.GetComponent<FairyLockTower>() : null;
                if (placed != null && script == null)
                    return "Fairy has a " + TowerName + " without the Fairy Lock Tower script: remove it and run again (Fairy unchanged)";
                bool linked = script != null && new SerializedObject(script).FindProperty("cage").objectReferenceValue == cage.gameObject;
                if (mesh == null && filter == null && dropFsm == null && candles.Count == 0 && placed != null && linked)
                    return "Fairy already done (nothing changed)";

                string backup = Backup(FairyPath);
                var changes = new List<string>();
                if (mesh != null || filter != null)
                {
                    if (mesh != null) Object.DestroyImmediate(mesh);
                    if (filter != null) Object.DestroyImmediate(filter);
                    changes.Add("The_Cage's bone mesh removed (its trigger, body and Start FSM kept)");
                }
                if (dropFsm != null)
                {
                    Object.DestroyImmediate(dropFsm);
                    changes.Add("The_Cage_Drop_Pos's FSM removed (no broken bones spawn)");
                }
                if (candles.Count > 0)
                {
                    foreach (var candle in candles) Object.DestroyImmediate(candle);
                    changes.Add(candles.Count + " Skull_Candle_Purple removed");
                }
                if (placed == null)
                {
                    var made = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab, root.transform);
                    made.name = TowerName;
                    made.transform.localPosition = new Vector3(0f, TowerLocalY, 0f);
                    made.transform.localRotation = Quaternion.identity;
                    made.transform.localScale = Vector3.one;
                    made.transform.SetSiblingIndex(cage.GetSiblingIndex() + 1);
                    script = made.GetComponent<FairyLockTower>();
                    changes.Add(TowerName + " added next to The_Cage");
                }
                if (!linked)
                {
                    var so = new SerializedObject(script);
                    so.FindProperty("cage").objectReferenceValue = cage.gameObject;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changes.Add("the tower watches The_Cage");
                }
                PrefabUtility.SaveAsPrefabAsset(root, FairyPath);
                return "Fairy: " + string.Join(", ", changes) + " (" + backup + ")";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // A copy of the prefab as it is now, once (a later run keeps the first copy: the one from before the change).
        private static string Backup(string assetPath)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-11", BackupArea);
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, Path.GetFileName(assetPath));
            if (File.Exists(to)) return "backup from an earlier run kept";
            File.Copy(assetPath, to);
            return "backed up to " + to;
        }
    }
}
