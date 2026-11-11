using System.Collections.Generic;
using System.IO;
using Game.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.PlayGround.Editor
{
    /// <summary>
    /// Tools > 429 Game > Balance 2026-11-10 > PlayGround And Portal FX (balance pass, user 2026-11-10):
    /// - Hat aura (item 15, "add an aura particle to the Recipe_Hat so it's easier to spot ... keep it light"): makes
    ///   Assets/Prefabs/Effects/Hat_Aura.prefab, a copy of Hat_Black's GrowthParticles (the table hats' glow the user
    ///   approved, its particles capped at half the view like Effect Pool's; Hat_Black, its material and Lana's prefabs
    ///   aren't changed), and puts one in Recipe_Hat as its
    ///   child Aura at (0, 0.3, 0), scale 0.13 (the table hats' 1.9 m blobs), 1 blob a second instead of 1.5, wired to
    ///   MagicHat > Aura (which shows it only while this round's recipe is unread).
    /// - Portal smoke (item 11, the portal "still uses the old smoke particles"): switches off Gate_Spawn_Circle's flat
    ///   Wisps (the 4 rooftop Spawn Circles follow) and puts Puff On Appear on the Spider_Tree variant's root, so each
    ///   spider-ling comes out of the portal with the shared 3D Smoke_Flash_Small_Puff (size 0.5, 0.1 m up), pooled.
    ///   Never Lana's Smoke_Blast. Spider_Smarter itself isn't changed.
    /// It sets absolute values, so it's safe to run again (Hat_Aura is made again from Hat_Black each time). Before it
    /// changes anything it copies those prefabs to Documents/429R_claude_backups/2026-11-10/playground/ (the first copy is
    /// kept). Close them in Prefab Mode first: an open one is skipped. Don't re-run _claude/unity_mcp/gate_fx_build.cs
    /// (outside the project): it makes Gate_Spawn_Circle again with its Wisps on.
    /// </summary>
    public static class BalancePlayGroundFxMenu
    {
        private const string MenuName = "PlayGround And Portal FX";
        private const string HatBlackPath = "Assets/Prefabs/Items/Hat_Black.prefab";
        private const string AuraPath = "Assets/Prefabs/Effects/Hat_Aura.prefab";
        private const string RecipeHatPath = "Assets/Prefabs/PlayGround/Recipe_Hat.prefab";
        private const string GateCirclePath = "Assets/Prefabs/Effects/Gates/Gate_Spawn_Circle.prefab";
        private const string SpiderTreePath = "Assets/Prefabs/Enemies/Enemies_Smarter/Spider_Tree.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string GlowName = "GrowthParticles";
        private const string AuraPrefabName = "Hat_Aura";
        private const string AuraName = "Aura";
        private const string WispsName = "Wisps";
        // The table hats' blobs are 14.7 x 0.13 (child 0.186 x hat 0.7) = 1.9 m; Recipe_Hat's root is scale 1.
        private const float AuraScale = 0.13f;
        private static readonly Vector3 AuraSpot = new(0f, 0.3f, 0f);
        // About 4 blobs out instead of 6: kept light (the user dislikes overdone particles).
        private const float AuraRate = 1f;
        private const float PuffSize = 0.5f;
        private const float PuffHeight = 0.1f;

        [MenuItem("Tools/429 Game/Balance 2026-11-10/PlayGround And Portal FX")]
        public static void Run()
        {
            var notes = new List<string>();
            string backups = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-10", "playground");
            Directory.CreateDirectory(backups);
            foreach (var path in new[] { AuraPath, RecipeHatPath, GateCirclePath, SpiderTreePath }) Backup(path, backups, notes);

            var aura = BuildAura(notes);
            if (aura != null) PutAuraOnHat(aura, notes);
            TurnOffWisps(notes);
            PuffSpiderLings(notes);

            AssetDatabase.SaveAssets();
            Debug.Log($"[{MenuName}] Done:\n" + string.Join("\n", notes));
        }

        // ---- Item 15: the hat aura ----

        // Hat_Aura: a copy of Hat_Black's GrowthParticles (capped on screen), at the middle of what it's on, sized for a
        // hat about 1 m across. Made again from Hat_Black on every run (Hat_Black is only read).
        private static GameObject BuildAura(List<string> notes)
        {
            if (OpenInPrefabMode(AuraPath))
            {
                notes.Add("Hat_Aura is open in Prefab Mode: not made again (close it and run again).");
                return AssetDatabase.LoadAssetAtPath<GameObject>(AuraPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HatBlackPath) == null)
            {
                notes.Add($"No {HatBlackPath}: no hat aura made.");
                return null;
            }

            var hatBlack = PrefabUtility.LoadPrefabContents(HatBlackPath);
            try
            {
                var glow = FindChild(hatBlack.transform, GlowName);
                if (glow == null || glow.GetComponent<ParticleSystem>() == null)
                {
                    notes.Add($"Hat_Black has no {GlowName} particles: no hat aura made.");
                    return null;
                }
                // Copied inside Hat_Black's own preview scene (so no open scene gets changed), then saved on its own.
                var copy = Object.Instantiate(glow.gameObject, hatBlack.transform);
                copy.transform.SetParent(null, false);
                copy.name = AuraPrefabName;
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one * AuraScale;
                // The player walks right up to this hat to slap it: its 1.9 m blobs (Hat_Black's are uncapped) would fill
                // the view layer on layer, what slows the headset most. Capped like the pooled effects (review, 2026-11-10).
                foreach (var look in copy.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    look.maxParticleSize = Mathf.Min(look.maxParticleSize, EffectPool.MaxScreenSize);
                var saved = PrefabUtility.SaveAsPrefabAsset(copy, AuraPath, out bool ok);
                Object.DestroyImmediate(copy);
                if (!ok || saved == null)
                {
                    notes.Add($"Couldn't save {AuraPath}: no hat aura made.");
                    return null;
                }
                notes.Add($"{AuraPath}: copy of Hat_Black/{GlowName}, scale {AuraScale}, particles capped at " +
                          $"{EffectPool.MaxScreenSize} of the view.");
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(hatBlack);
            }
        }

        // Recipe_Hat gets one as its child Aura, at the hat's middle, lighter (Aura Rate), wired to its MagicHat.
        private static void PutAuraOnHat(GameObject aura, List<string> notes)
        {
            if (!Loadable(RecipeHatPath, notes)) return;
            var root = PrefabUtility.LoadPrefabContents(RecipeHatPath);
            try
            {
                var hat = root.GetComponent<MagicHat>();
                if (hat == null)
                {
                    notes.Add("Recipe_Hat has no MagicHat on its root: no aura put on it.");
                    return;
                }

                // Its Aura from an earlier run is kept and set again; anything else called Aura there is replaced.
                GameObject made = null;
                var old = root.transform.Find(AuraName);
                if (old != null)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(old.gameObject) &&
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject) == AuraPath)
                    {
                        made = old.gameObject;
                    }
                    else if (PrefabUtility.IsAddedGameObjectOverride(old.gameObject))
                    {
                        Object.DestroyImmediate(old.gameObject);
                    }
                    else
                    {
                        notes.Add("Recipe_Hat: its child Aura isn't a Hat_Aura and can't be removed here: no aura put on it.");
                        return;
                    }
                }
                if (made == null)
                {
                    made = (GameObject)PrefabUtility.InstantiatePrefab(aura, root.transform);
                    made.name = AuraName;
                }

                var spot = made.transform;
                spot.localPosition = AuraSpot;
                spot.localRotation = Quaternion.identity;
                spot.localScale = Vector3.one * AuraScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(spot);

                var glow = made.GetComponent<ParticleSystem>();
                var particles = new SerializedObject(glow);
                var mode = particles.FindProperty("EmissionModule.rateOverTime.minMaxState");
                var rate = particles.FindProperty("EmissionModule.rateOverTime.scalar");
                if (mode != null && rate != null)
                {
                    mode.intValue = (int)ParticleSystemCurveMode.Constant;
                    rate.floatValue = AuraRate;
                    particles.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    var emission = glow.emission;
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(AuraRate);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(glow);
                }

                var hatProps = new SerializedObject(hat);
                var field = hatProps.FindProperty("aura");
                if (field == null)
                {
                    notes.Add("MagicHat has no Aura field yet: let Unity compile the scripts, then run this again.");
                }
                else
                {
                    field.objectReferenceValue = glow;
                    hatProps.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, RecipeHatPath, out bool ok);
                notes.Add(ok
                    ? $"Recipe_Hat: child {AuraName} (Hat_Aura) at {AuraSpot}, scale {AuraScale}, {AuraRate} blob a second, MagicHat > Aura set."
                    : "Couldn't save Recipe_Hat.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- Item 11: the rooftop portals' smoke ----

        // The portal's flat purple wisps (Portal Smoke.mat, the old smoke) go. The 4 rooftop Spawn Circles have no
        // overrides on them, so they all follow.
        private static void TurnOffWisps(List<string> notes)
        {
            if (!Loadable(GateCirclePath, notes)) return;
            var root = PrefabUtility.LoadPrefabContents(GateCirclePath);
            try
            {
                var wisps = FindChild(root.transform, WispsName);
                if (wisps == null)
                {
                    notes.Add("Gate_Spawn_Circle has no Wisps: nothing to switch off.");
                    return;
                }
                // Saved only when it changes, so a second run doesn't rewrite the prefab.
                if (!wisps.gameObject.activeSelf)
                {
                    notes.Add("Gate_Spawn_Circle: Wisps already off.");
                    return;
                }
                wisps.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, GateCirclePath, out bool ok);
                notes.Add(ok ? "Gate_Spawn_Circle: Wisps (flat Portal Smoke) switched off." : "Couldn't save Gate_Spawn_Circle.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Each spider-ling puffs the shared 3D smoke as it comes out of a portal (an added component on the variant).
        private static void PuffSpiderLings(List<string> notes)
        {
            var smoke = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            if (smoke == null)
            {
                notes.Add($"No {PuffPath}: no spider-ling puff.");
                return;
            }
            if (!Loadable(SpiderTreePath, notes)) return;
            var root = PrefabUtility.LoadPrefabContents(SpiderTreePath);
            try
            {
                bool changed = false;
                var puff = root.GetComponent<PuffOnAppear>();
                if (puff == null)
                {
                    puff = root.AddComponent<PuffOnAppear>();
                    changed = true;
                }
                var props = new SerializedObject(puff);
                changed |= Set(props, "effect", smoke);
                changed |= Set(props, "size", PuffSize);
                changed |= Set(props, "height", PuffHeight);
                props.ApplyModifiedPropertiesWithoutUndo();
                // Saved only when it changes (Spider_Tree's FSMs come from Spider_Smarter; no need to rewrite it).
                if (!changed)
                {
                    notes.Add("Spider_Tree: Puff On Appear already set.");
                    return;
                }
                PrefabUtility.SaveAsPrefabAsset(root, SpiderTreePath, out bool ok);
                notes.Add(ok
                    ? $"Spider_Tree: Puff On Appear (Smoke_Flash_Small_Puff, size {PuffSize}, {PuffHeight} m up)."
                    : "Couldn't save Spider_Tree.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- Helpers ----

        // The first copy (from before any run) is kept: later runs don't overwrite it.
        private static void Backup(string assetPath, string folder, List<string> notes)
        {
            string from = Path.GetFullPath(assetPath);
            if (!File.Exists(from)) return;
            string to = Path.Combine(folder, Path.GetFileName(assetPath));
            if (File.Exists(to)) return;
            File.Copy(from, to);
            if (File.Exists(from + ".meta")) File.Copy(from + ".meta", to + ".meta", true);
            notes.Add($"Backed up {assetPath} to {folder}.");
        }

        // The prefab is there and not open in Prefab Mode (a save here would be lost under the open copy).
        private static bool Loadable(string path, List<string> notes)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add($"No {path}: skipped.");
                return false;
            }
            if (OpenInPrefabMode(path))
            {
                notes.Add($"{Path.GetFileNameWithoutExtension(path)} is open in Prefab Mode: skipped (close it and run again).");
                return false;
            }
            return true;
        }

        private static bool OpenInPrefabMode(string path)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.assetPath == path;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == childName) return t;
            }
            return null;
        }

        private static bool Set(SerializedObject props, string name, Object value)
        {
            var p = props.FindProperty(name);
            if (p == null || p.objectReferenceValue == value) return false;
            p.objectReferenceValue = value;
            return true;
        }

        private static bool Set(SerializedObject props, string name, float value)
        {
            var p = props.FindProperty(name);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }
    }
}
