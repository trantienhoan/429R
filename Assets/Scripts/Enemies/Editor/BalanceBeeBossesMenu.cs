using System.Collections.Generic;
using System.IO;
using Game.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Balance 2026-11-10 > Bee Bosses Stay High (user, 2026-11-10: the bee bosses were easy, fighting
    /// at chest height). Sets these on the prefabs; they're absolute, so running it again gives the same result:
    /// - Bee_Poison_Sting (made once, a variant of Spider_Web_Ball): a bright violet ball with a short trail that flies
    ///   straight (no gravity); it hurts like a bug bite (Damage), adds no web, and poisons the player 2 a second for 5 s
    ///   (Player Poison);
    /// - Bee_Boss: Stays High, Cruise Height 3-3.6 m, circling 3.5 m out at 45 degrees a second, High Wait 12-18 s (the
    ///   rare dive), wings kept all fight (Ground Below Health 0), and it shoots the poison sting every 2.5-4 s from
    ///   2.5-14 m (never point-blank right after a dive); its dive Sting poisons too (3 a second for 4 s, on top of its hit);
    /// - Bee_Boss_PumpkinButt: inherits all of that and shoots more often (every 1.8-3 s).
    /// The code side is Spider Brain (Stays High, the shot from the flier's height), Web Ball and Player Poison. Every
    /// asset is copied to Documents/429R_claude_backups/2026-11-10/bees/ before it's changed (a later run keeps the first
    /// run's copies, the ones from before the pass). No scene change: the bosses come out of the whack 6/7 barrels.
    /// </summary>
    public static class BalanceBeeBossesMenu
    {
        private const string Smarter = "Assets/Prefabs/Enemies/Enemies_Smarter/";
        private const string WebBallPath = Smarter + "Spider_Web_Ball.prefab";
        private const string StingPath = Smarter + "Bee_Poison_Sting.prefab";
        private const string BossPath = Smarter + "Bee_Boss.prefab";
        private const string PumpkinButtPath = Smarter + "Bee_Boss_PumpkinButt.prefab";
        private const string EffectsFolder = "Assets/Data/Effects/";
        private const string BodyMaterialPath = EffectsFolder + "Bee Poison Sting.mat";
        private const string TrailMaterialName = "Bee Poison Sting Trail";
        // Danger Flame's helper makes the trail's additive glow there; no texture: one even colour, like the bow's trail.
        private const string GlowTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Circle01.png";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string StingSoundPath = "Assets/Audio/SFX/bug_Attack.wav";
        private const string PopSoundPath = "Assets/Audio/SFX/bubble_pop_1.wav";
        private const string BackupArea = "bees";
        private const string LogTitle = "[Balance 2026-11-10 Bee Bosses]";

        // The sting shot. A sphere 10 cm across at size 1 (a bee boss shoots it 1.6 times as big: 16 cm), hitting things
        // a little further out than it shows (Radius 0.6 x its scale: about 10 cm at the boss's size).
        private static readonly Color StingColour = new(0.72f, 0.3f, 1f);
        private static readonly Color TrailColour = new(0.62f, 0.25f, 0.85f);
        private const float StingSize = 0.1f;
        private const float StingRadius = 0.6f;
        private const float StingLifetime = 3f;
        private const float StingSplatScale = 0.02f;
        private const float StingPoisonPerSecond = 2f;
        private const float StingPoisonSeconds = 5f;
        private const float TrailSeconds = 0.12f;
        private const float TrailWidth = 0.07f;

        // Bee_Boss: up high, shooting, diving only now and then.
        private static readonly Vector2 CruiseHeight = new(3f, 3.6f);
        private const float CircleDistance = 3.5f;
        private const float CircleSpeed = 45f;
        private static readonly Vector2 HighWait = new(12f, 18f);
        private const float GroundBelowHealth = 0f;
        private const string WebAnimation = "attack";
        private const float WebReleaseTime = 0.4f;
        private const float WebDuration = 0.9f;
        // From 2.5 m: it circles 3.5 m out, so its usual shots still fly, but right after a dive (about 1 m away, over
        // the player's head) it can't fire one that's too close to dodge or punch away (review, 2026-11-10).
        private static readonly Vector2 WebRange = new(2.5f, 14f);
        private static readonly Vector2 WebCooldown = new(2.5f, 4f);
        private const float WebSpeed = 7f;
        private const int MaxWebShooters = 3;
        private const string StingAttack = "Sting";
        private const float DivePoisonPerSecond = 3f;
        private const float DivePoisonSeconds = 4f;

        // Bee_Boss_PumpkinButt shoots more often; everything else it inherits from Bee_Boss.
        private static readonly Vector2 PumpkinButtWebCooldown = new(1.8f, 3f);
        private static readonly string[] Inherited =
        {
            "staysHigh", "cruiseHeight.x", "cruiseHeight.y", "circleDistance", "highWait.x", "highWait.y",
            "groundBelowHealth", "webBall", "boss.webs", "webRange.x", "webRange.y",
        };

        [MenuItem("Tools/429 Game/Balance 2026-11-10/Bee Bosses Stay High")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(LogTitle + " Stop Play mode first: it changes prefabs.");
                return;
            }
            var log = new List<string>();
            var stingSound = AssetDatabase.LoadAssetAtPath<AudioClip>(StingSoundPath);
            var popSound = AssetDatabase.LoadAssetAtPath<AudioClip>(PopSoundPath);
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);

            // The shot first, so the bosses can point at it.
            var body = BodyMaterial(log);
            var trail = TrailMaterial(log);
            bool madeSting = MakeSting(log);
            log.Add(Change(StingPath, !madeSting, (root, notes) => SetUpSting(root, body, trail, puff, stingSound, popSound, notes)));
            var sting = AssetDatabase.LoadAssetAtPath<GameObject>(StingPath);

            log.Add(Change(BossPath, true, (root, notes) =>
            {
                var brain = Of<SpiderBrain>(root, notes);
                if (brain == null) return false;
                bool changed = SetBool(brain, "staysHigh", true, notes);
                changed |= SetVector2(brain, "cruiseHeight", CruiseHeight, notes);
                changed |= SetFloat(brain, "circleDistance", CircleDistance, notes);
                changed |= SetFloat(brain, "circleSpeed", CircleSpeed, notes);
                changed |= SetVector2(brain, "highWait", HighWait, notes);
                changed |= SetFloat(brain, "groundBelowHealth", GroundBelowHealth, notes);
                changed |= SetObject(brain, "webBall", sting, notes);
                changed |= SetBool(brain, "boss.webs", true, notes);
                changed |= SetString(brain, "webAnimation", WebAnimation, notes);
                // No bone to grow the shot on (that's the spider's web): it appears at its mouth as it's shot.
                changed |= SetString(brain, "webHoldBone", "", notes);
                changed |= SetFloat(brain, "webReleaseTime", WebReleaseTime, notes);
                changed |= SetFloat(brain, "webDuration", WebDuration, notes);
                changed |= SetVector2(brain, "webRange", WebRange, notes);
                changed |= SetVector2(brain, "webCooldown", WebCooldown, notes);
                changed |= SetFloat(brain, "webSpeed", WebSpeed, notes);
                changed |= SetInt(brain, "maxWebShooters", MaxWebShooters, notes);
                changed |= SetObject(brain, "webSound", stingSound, notes);
                changed |= SetAttackPoison(brain, StingAttack, DivePoisonPerSecond, DivePoisonSeconds, notes);
                brain.ApplyModifiedPropertiesWithoutUndo();
                return changed;
            }));

            log.Add(Change(PumpkinButtPath, true, (root, notes) =>
            {
                var brain = Of<SpiderBrain>(root, notes);
                if (brain == null) return false;
                bool changed = SetVector2(brain, "webCooldown", PumpkinButtWebCooldown, notes);
                brain.ApplyModifiedPropertiesWithoutUndo();
                return changed;
            }));
            log.Add(CheckInherits(PumpkinButtPath, Inherited));

            AssetDatabase.SaveAssets();
            Debug.Log(LogTitle + "\n" + string.Join("\n", log));
        }

        // ---- the sting shot ----

        // Bee_Poison_Sting, once: a variant of Spider_Web_Ball, so the web ball's later fixes carry over. True when made now.
        private static bool MakeSting(List<string> log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(StingPath);
            if (existing != null)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(existing);
                if (PrefabUtility.GetPrefabAssetType(existing) != PrefabAssetType.Variant || source == null ||
                    AssetDatabase.GetAssetPath(source) != WebBallPath)
                    log.Add("Bee_Poison_Sting: not a variant of Spider_Web_Ball any more (unpacked?); its values are set anyway");
                return false;
            }
            var webBall = AssetDatabase.LoadAssetAtPath<GameObject>(WebBallPath);
            if (webBall == null)
            {
                log.Add("Bee_Poison_Sting: Spider_Web_Ball is missing, so it can't be made");
                return false;
            }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(webBall, scene);
                instance.name = "Bee_Poison_Sting";
                PrefabUtility.SaveAsPrefabAsset(instance, StingPath);
                log.Add("Bee_Poison_Sting: made (a variant of Spider_Web_Ball)");
                return true;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // The shot's look and numbers. Always saved: it's this menu's own prefab, and saving it unchanged changes nothing.
        private static bool SetUpSting(GameObject root, Material body, Material trailMaterial, GameObject puff, AudioClip stingSound,
            AudioClip popSound, List<string> notes)
        {
            root.transform.localScale = Vector3.one * StingSize;
            notes.Add($"scale {StingSize} (a {StingSize * 100f:0} cm ball at size 1)");

            var sphere = SphereMesh();
            if (root.TryGetComponent(out MeshFilter filter) && sphere != null) filter.sharedMesh = sphere;
            else notes.Add("no sphere mesh or Mesh Filter: its mesh left as it is");
            if (root.TryGetComponent(out MeshRenderer renderer))
            {
                if (body != null) renderer.sharedMaterial = body;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            // It flies straight: Spider Brain aims it without gravity when its Rigidbody has none.
            if (root.TryGetComponent(out Rigidbody rigidbody)) rigidbody.useGravity = false;
            notes.Add("sphere, violet, no shadows, no gravity");

            var found = root.transform.Find("Trail");
            GameObject trailGo;
            if (found != null) trailGo = found.gameObject;
            else
            {
                trailGo = new GameObject("Trail");
                trailGo.transform.SetParent(root.transform, false);
            }
            trailGo.transform.localPosition = Vector3.zero;
            if (!trailGo.TryGetComponent(out TrailRenderer trail)) trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = TrailSeconds;
            trail.minVertexDistance = 0.03f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = TrailWidth;
            trail.colorGradient = Fade(TrailColour, 0.9f);
            if (trailMaterial != null) trail.sharedMaterial = trailMaterial;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = true;
            trail.autodestruct = false;
            notes.Add($"trail {TrailSeconds} s, {TrailWidth * 100f:0} cm wide");

            var web = Of<WebBall>(root, notes);
            if (web != null)
            {
                SetString(web, "playerEvent", "Damage", notes);
                // No web on the player: it's poison, not web.
                SetFloat(web, "webAdded", 0f, notes);
                SetFloat(web, "poisonPerSecond", StingPoisonPerSecond, notes);
                SetFloat(web, "poisonSeconds", StingPoisonSeconds, notes);
                SetFloat(web, "radius", StingRadius, notes);
                SetFloat(web, "lifetime", StingLifetime, notes);
                SetObject(web, "splatEffect", puff, notes);
                SetFloat(web, "splatScale", StingSplatScale, notes);
                SetObject(web, "hitSound", stingSound, notes);
                SetObject(web, "splatSound", popSound, notes);
                SetFloat(web, "volume", 1f, notes);
                web.ApplyModifiedPropertiesWithoutUndo();
            }
            return true;
        }

        // Unity's own sphere (1 m across), the cheapest round ball.
        private static Mesh SphereMesh()
        {
            try
            {
                var mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                if (mesh != null) return mesh;
            }
            catch (System.Exception)
            {
                // Not found under that name: made from a primitive below.
            }
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var shared = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return shared;
        }

        // The ball: one flat bright violet, unlit (no light needed to read it in the dark rooms; cheap on the headset).
        private static Material BodyMaterial(List<string> log)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    log.Add("Bee Poison Sting.mat: the URP Unlit shader wasn't found, so the sting keeps the web ball's look");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, BodyMaterialPath);
                log.Add("Bee Poison Sting.mat: made");
            }
            else log.Add("Bee Poison Sting.mat: " + Backup(BodyMaterialPath));
            material.SetColor("_BaseColor", StingColour);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The trail: an even additive glow, like the light arrow's.
        private static Material TrailMaterial(List<string> log)
        {
            string path = EffectsFolder + TrailMaterialName + ".mat";
            log.Add(TrailMaterialName + ".mat: " + (File.Exists(path) ? Backup(path) : "made"));
            var material = DangerFlameSetupMenu.MakeMaterial(TrailMaterialName, GlowTexture, true);
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", TrailColour);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Gradient Fade(Color colour, float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(colour, 0.3f), new GradientColorKey(colour, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        // ---- shared bits ----

        // Loads the prefab, lets Edit change it and saves it only if something changed (a backup copy first, unless this
        // run just made it).
        private static string Change(string path, bool backUp, System.Func<GameObject, List<string>, bool> edit)
        {
            string label = Path.GetFileNameWithoutExtension(path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return label + ": missing, skipped";
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) return label + ": open in Prefab Mode, skipped (close it and run again)";
            string backup = backUp ? Backup(path) : "made this run, no backup needed";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var notes = new List<string>();
                bool changed = edit(root, notes);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{label}: {string.Join(", ", notes)}{(changed ? ", saved" : ", nothing to change")} ({backup})";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static SerializedObject Of<T>(GameObject root, List<string> notes) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component != null) return new SerializedObject(component);
            notes.Add("no " + typeof(T).Name + " on its root");
            return null;
        }

        private static SerializedProperty Find(SerializedObject so, string property, List<string> notes)
        {
            var p = so.FindProperty(property);
            if (p == null) notes.Add(property + " not found");
            return p;
        }

        private static bool SetFloat(SerializedObject so, string property, float value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (Mathf.Approximately(p.floatValue, value))
            {
                notes.Add($"{property} {value} (already)");
                return false;
            }
            notes.Add($"{property} {p.floatValue} -> {value}");
            p.floatValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject so, string property, int value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (p.intValue == value)
            {
                notes.Add($"{property} {value} (already)");
                return false;
            }
            notes.Add($"{property} {p.intValue} -> {value}");
            p.intValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject so, string property, bool value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (p.boolValue == value)
            {
                notes.Add($"{property} {value} (already)");
                return false;
            }
            notes.Add($"{property} {p.boolValue} -> {value}");
            p.boolValue = value;
            return true;
        }

        private static bool SetString(SerializedObject so, string property, string value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (p.stringValue == value)
            {
                notes.Add($"{property} '{value}' (already)");
                return false;
            }
            notes.Add($"{property} '{p.stringValue}' -> '{value}'");
            p.stringValue = value;
            return true;
        }

        private static bool SetVector2(SerializedObject so, string property, Vector2 value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (Mathf.Approximately(p.vector2Value.x, value.x) && Mathf.Approximately(p.vector2Value.y, value.y))
            {
                notes.Add($"{property} {value.x}-{value.y} (already)");
                return false;
            }
            notes.Add($"{property} {p.vector2Value.x}-{p.vector2Value.y} -> {value.x}-{value.y}");
            p.vector2Value = value;
            return true;
        }

        private static bool SetObject(SerializedObject so, string property, Object value, List<string> notes)
        {
            var p = Find(so, property, notes);
            if (p == null) return false;
            if (value == null)
            {
                notes.Add(property + ": its asset is missing, left as it is");
                return false;
            }
            if (p.objectReferenceValue == value)
            {
                notes.Add($"{property} {value.name} (already)");
                return false;
            }
            notes.Add($"{property} -> {value.name}");
            p.objectReferenceValue = value;
            return true;
        }

        // The poison on one attack, found by its name (saved as an override of that list entry on this prefab).
        private static bool SetAttackPoison(SerializedObject so, string attackName, float perSecond, float seconds, List<string> notes)
        {
            var attacks = Find(so, "attacks", notes);
            if (attacks == null) return false;
            for (int i = 0; i < attacks.arraySize; i++)
            {
                var attack = attacks.GetArrayElementAtIndex(i);
                if (attack.FindPropertyRelative("name").stringValue != attackName) continue;
                string at = $"attacks[{i}] ({attackName}).";
                bool changed = false;
                foreach (var (field, value) in new[] { ("poisonPerSecond", perSecond), ("poisonSeconds", seconds) })
                {
                    var p = attack.FindPropertyRelative(field);
                    if (p == null)
                    {
                        notes.Add(at + field + " not found");
                        continue;
                    }
                    if (Mathf.Approximately(p.floatValue, value))
                    {
                        notes.Add($"{at}{field} {value} (already)");
                        continue;
                    }
                    notes.Add($"{at}{field} {p.floatValue} -> {value}");
                    p.floatValue = value;
                    changed = true;
                }
                return changed;
            }
            notes.Add($"no '{attackName}' attack: its poison isn't set");
            return false;
        }

        // A variant that should inherit these values: says so, or warns about any it has its own of.
        private static string CheckInherits(string path, string[] properties)
        {
            string label = Path.GetFileNameWithoutExtension(path);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return label + ": missing";
            var own = new List<string>();
            foreach (var change in PrefabUtility.GetPropertyModifications(go) ?? System.Array.Empty<PropertyModification>())
            {
                if (!(change.target is SpiderBrain)) continue;
                if (System.Array.IndexOf(properties, change.propertyPath) >= 0)
                    own.Add($"{change.propertyPath} {(change.objectReference != null ? change.objectReference.name : change.value)}");
            }
            return own.Count == 0 ? $"{label}: inherits Bee_Boss's Stays High, heights, waits, wings and shot"
                : $"{label}: has its own {string.Join(", ", own)} (not inherited from Bee_Boss; left as it is)";
        }

        // A copy of the asset as it is now, once (a later run keeps the first copy: the one from before the pass).
        private static string Backup(string assetPath)
        {
            string folder = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "429R_claude_backups", "2026-11-10", BackupArea);
            Directory.CreateDirectory(folder);
            string to = Path.Combine(folder, Path.GetFileName(assetPath));
            if (File.Exists(to)) return "backup from an earlier run kept";
            File.Copy(assetPath, to);
            return "backed up to " + to;
        }
    }
}
