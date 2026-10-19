using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>Menu command that turns the head-locked heart into the heart-and-number health HUD (Tools > 429 Game > Player).</summary>
    public static class PlayerHealthHUDSetupMenu
    {
        private const string HeartSprite = "Assets/Images/heart_white.png";
        private const string ShineSprite = "Assets/Images/heart_shine.png";
        private const string CrackSprite = "Assets/Images/heart_crack.png";
        private const string MaterialFolder = "Assets/Data/UI";
        private const string ImageMaterialPath = MaterialFolder + "/HUD Image Overlay.mat";
        private const string TextMaterialPath = MaterialFolder + "/HUD Text Overlay.mat";
        private const string ImageShader = "429/UI Overlay";
        private const string TextShader = "TextMeshPro/Mobile/Distance Field Overlay";
        private const string SparkleTexture = "Assets/Images/hud_sparkle.png";
        private const string SparkleMaterialPath = MaterialFolder + "/HUD Sparkle.mat";
        private const string SparkleShader = "429/Particles Overlay Additive";
        private static readonly Color BonusGreen = new(0.3f, 1f, 0.42f);

        /// <summary>
        /// Reworks the HUD canvas under the Main Camera (the one holding the PlayerHealth FSM's "HealthBar" slider) in
        /// place, so the FSM's links to it keep working: a grey heart tinted red (drains from the top) over a dark one,
        /// a shine on top, the health as a number, a "-8" / "+5" pop, a dark plate behind, all drawn over the scene.
        /// Adds a PlayerHealthHUD that reads the FSM. The slider no longer drives the heart. Safe to run again.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Set Up Health HUD")]
        public static void SetUp()
        {
            PlayMakerFSM fsm = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.FsmName == "PlayerHealth") { fsm = f; break; }
            }
            Slider bar = null;
            foreach (var s in Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (s.name == "HealthBar") { bar = s; break; }
            }
            var canvas = bar != null ? bar.GetComponentInParent<Canvas>(true) : null;
            if (fsm == null || canvas == null)
            {
                Debug.LogError("[Health HUD] Needs the PlayerHealth FSM and the 'HealthBar' slider on a canvas (Main Camera > Canvas) in the open scene. Nothing changed.");
                return;
            }
            var heartSprite = PrepareSprite(HeartSprite);
            var shineSprite = PrepareSprite(ShineSprite);
            var imageShader = Shader.Find(ImageShader);
            var textShader = Shader.Find(TextShader);
            var sparkleShader = Shader.Find(SparkleShader);
            var sparkleTexture = PrepareTexture(SparkleTexture);
            var font = SetupUtility.ResolveFont();
            if (heartSprite == null || shineSprite == null || imageShader == null || textShader == null || font == null || sparkleShader == null || sparkleTexture == null)
            {
                Debug.LogError($"[Health HUD] Missing one of: {HeartSprite}, {ShineSprite}, shader '{ImageShader}' (Assets/Shader/UIOverlay.shader), " +
                               $"shader '{TextShader}', a TextMeshPro font, {SparkleTexture}, shader '{SparkleShader}'. Nothing changed.");
                return;
            }
            SetupUtility.EnsureFolder(MaterialFolder);
            var imageMaterial = GetOrCreateMaterial(ImageMaterialPath, () => new Material(imageShader) { name = "HUD Image Overlay" });
            var textMaterial = GetOrCreateMaterial(TextMaterialPath, () =>
            {
                var m = new Material(font.material) { name = "HUD Text Overlay" };
                m.shader = textShader;
                return m;
            });
            var sparkleMaterial = GetOrCreateMaterial(SparkleMaterialPath, () => new Material(sparkleShader) { name = "HUD Sparkle" });
            sparkleMaterial.shader = sparkleShader;
            sparkleMaterial.mainTexture = sparkleTexture;
            sparkleMaterial.renderQueue = 3999;
            EditorUtility.SetDirty(sparkleMaterial);
            imageMaterial.renderQueue = 3998;         // HUD 3998, its sparkles 3999 (over the heart), headset fade 4000
            textMaterial.renderQueue = 3998;         // under the headset fade (4000), so the death fade-out covers the HUD
            EditorUtility.SetDirty(imageMaterial);
            EditorUtility.SetDirty(textMaterial);

            var root = canvas.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(root, "Set Up Health HUD");
            root.SetActive(true);                    // the HUD fades itself out; the FSM still switches it on and off too
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(380f, 150f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            foreach (var raycaster in root.GetComponents<BaseRaycaster>()) raycaster.enabled = false;   // nothing on it to click

            var plate = FindOrCreateImage(rect, "Plate");
            plate.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 0.4f;    // rounder corners
            plate.color = new Color(0f, 0f, 0f, 0.45f);
            plate.material = imageMaterial;
            Centre(plate.rectTransform, Vector2.zero, new Vector2(380f, 150f));
            plate.transform.SetAsFirstSibling();

            // The heart: the slider object stays (the FSM still sets its value), but no longer moves the fill.
            bar.fillRect = null;
            bar.handleRect = null;
            bar.interactable = false;
            bar.transition = Selectable.Transition.None;
            var heart = (RectTransform)bar.transform;
            heart.localScale = Vector3.one;
            heart.localRotation = Quaternion.identity;
            Centre(heart, new Vector2(-105f, 0f), new Vector2(120f, 120f));
            heart.SetSiblingIndex(1);

            var empty = FindOrCreateImage(heart, "Tank");
            var fill = FindOrCreateImage(heart, "Fill");
            var shine = FindOrCreateImage(heart, "Shine");
            foreach (var image in new[] { empty, fill, shine })
            {
                image.material = imageMaterial;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                var r = image.rectTransform;
                r.localScale = Vector3.one;
                SetupUtility.Stretch(r, Vector2.zero, Vector2.zero);
            }
            empty.sprite = heartSprite;
            empty.color = new Color(0.22f, 0.04f, 0.06f, 0.92f);
            fill.sprite = heartSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;
            fill.color = new Color(0.93f, 0.16f, 0.2f);
            shine.sprite = shineSprite;
            shine.color = new Color(1f, 1f, 1f, 0.85f);
            empty.transform.SetSiblingIndex(0);
            fill.transform.SetSiblingIndex(1);
            shine.transform.SetSiblingIndex(2);

            var sparkles = CreateSparkles(heart, sparkleMaterial);

            var number = FindOrCreateText(rect, "Health Number", "120", 84f, font);
            Centre(number.rectTransform, new Vector2(78f, -4f), new Vector2(220f, 120f));
            number.fontSharedMaterial = textMaterial;
            var change = FindOrCreateText(rect, "Health Change", "-8", 50f, font);
            Centre(change.rectTransform, new Vector2(82f, 52f), new Vector2(200f, 60f));
            change.fontSharedMaterial = textMaterial;
            change.enabled = false;

            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(root);
            group.interactable = false;
            group.blocksRaycasts = false;
            var hud = root.GetComponent<PlayerHealthHUD>();
            bool added = hud == null;
            if (added) hud = Undo.AddComponent<PlayerHealthHUD>(root);
            SetupUtility.SetReference(hud, "healthFsm", fsm);
            SetupUtility.SetReference(hud, "heartFill", fill);
            SetupUtility.SetReference(hud, "heartEmpty", empty);
            SetupUtility.SetReference(hud, "heart", heart);
            SetupUtility.SetReference(hud, "number", number);
            SetupUtility.SetReference(hud, "change", change);
            SetupUtility.SetReference(hud, "bonusSparkles", sparkles);
            var hudSettings = new SerializedObject(hud);
            hudSettings.FindProperty("bonusColor").colorValue = BonusGreen;
            hudSettings.ApplyModifiedProperties();
            hud.Place();
            AddCrack(hud);
            ShowHeals(fsm, root);
            BringBackAfterHiding(fsm, root);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log($"[Health HUD] {(added ? "Set up" : "Updated")} on '{root.name}' (under '{root.transform.parent.name}'): heart + number, " +
                      "drawn over the scene, green with sparkles above the starting health, shows for 3 s after any change and stays up at low health. Distance, angles, colours " +
                      "and timings are on the Player Health HUD component. Save the scene.");
        }

        /// <summary>
        /// Updates a health HUD set up before: adds the cracked heart (shown once the player has died and got back up
        /// this run), and makes every heal show the HUD: the PlayerHealth FSM's heal state (Health1) switches
        /// the HUD on first, like the hits do, and Off Canvas no longer switches it off 3 s later (the HUD fades by
        /// itself). FSMs that hide the HUD (the XR Origin's Info on Game Over) switch it on again in their next state,
        /// so it's back after dying. Leaves everything else as it is. Safe to run again.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Update Health HUD (Cracked Heart, Show Heals)")]
        public static void UpdateHud()
        {
            var hud = Object.FindAnyObjectByType<PlayerHealthHUD>(FindObjectsInactive.Include);
            PlayMakerFSM fsm = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.FsmName == "PlayerHealth") { fsm = f; break; }
            }
            if (hud == null || fsm == null)
            {
                Debug.LogError("[Health HUD] Needs the Player Health HUD and the PlayerHealth FSM in the open scene (run Set Up Health HUD first). Nothing changed.");
                return;
            }

            var notes = new List<string>();
            if (AddCrack(hud)) notes.Add("added the cracked heart");
            int changed = ShowHeals(fsm, hud.gameObject);
            if (changed > 0) notes.Add($"heals now show the HUD ({changed} PlayerHealth state(s) changed)");
            int back = BringBackAfterHiding(fsm, hud.gameObject);
            if (back > 0) notes.Add($"the HUD comes back after being hidden, e.g. after dying ({back} state(s) changed)");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Health HUD] " + (notes.Count > 0 ? string.Join("; ", notes) : "already up to date") + ". Save the scene.", hud);
        }

        [MenuItem("Tools/429 Game/Player/Update Health HUD (Cracked Heart, Show Heals)", true)]
        private static bool CanUpdateHud() => !EditorApplication.isPlaying;

        // The crack over the heart, between its fill and its shine; hidden until the player has died this run.
        private static bool AddCrack(PlayerHealthHUD hud)
        {
            var settings = new SerializedObject(hud);
            if (settings.FindProperty("crack").objectReferenceValue != null) return false;
            var heart = settings.FindProperty("heart").objectReferenceValue as RectTransform;
            var fill = settings.FindProperty("heartFill").objectReferenceValue as Image;
            var sprite = PrepareSprite(CrackSprite);
            if (heart == null || sprite == null)
            {
                Debug.LogWarning($"[Health HUD] No heart on the HUD or no '{CrackSprite}', so the cracked heart wasn't added.", hud);
                return false;
            }

            var crack = FindOrCreateImage(heart, "Crack");
            crack.sprite = sprite;
            crack.color = Color.white;
            crack.material = fill != null ? fill.material : null;
            crack.preserveAspect = true;
            crack.raycastTarget = false;
            crack.type = Image.Type.Simple;
            crack.rectTransform.localScale = Vector3.one;
            SetupUtility.Stretch(crack.rectTransform, Vector2.zero, Vector2.zero);
            crack.transform.SetSiblingIndex(fill != null ? fill.transform.GetSiblingIndex() + 1 : heart.childCount - 1);
            crack.enabled = false;
            SetupUtility.SetReference(hud, "crack", crack);
            return true;
        }

        // Health1 (a heal) switches the HUD on first, as the hits do; Off Canvas no longer switches it off.
        private static int ShowHeals(PlayMakerFSM fsm, GameObject canvas)
        {
            if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
            foreach (var state in fsm.FsmStates) state.LoadActions();
            int changed = 0;

            var heal = fsm.FsmStates.FirstOrDefault(s => s.Name == "Health1");
            if (heal != null && SwitchOnFirst(heal, canvas)) changed++;

            var off = fsm.FsmStates.FirstOrDefault(s => s.Name == "Off Canvas");
            if (off != null && off.Actions.Any(a => Switches(a, canvas, false)))
            {
                off.Actions = off.Actions.Where(a => !Switches(a, canvas, false)).ToArray();
                changed++;
            }

            if (changed > 0)
            {
                Undo.RecordObject(fsm, "Show Heals On The Health HUD");
                foreach (var state in fsm.FsmStates) state.SaveActions();
                EditorUtility.SetDirty(fsm);
            }
            return changed;
        }

        // Other FSMs that hide the HUD (the XR Origin's Info hides it on Game Over, in Deactivate All) switch it on again
        // in the state they go to next (Init), so after dying the heart is back, cracked.
        private static int BringBackAfterHiding(PlayMakerFSM health, GameObject canvas)
        {
            int changed = 0;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fsm == health) continue;
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                bool changedHere = false;
                foreach (var state in fsm.FsmStates)
                {
                    state.LoadActions();
                    if (!state.Actions.Any(a => Switches(a, canvas, false))) continue;
                    foreach (var transition in state.Transitions)
                    {
                        var next = fsm.FsmStates.FirstOrDefault(s => s.Name == transition.ToState);
                        if (next == null || next == state) continue;
                        next.LoadActions();
                        if (SwitchOnFirst(next, canvas)) { changedHere = true; changed++; }
                    }
                }
                if (!changedHere) continue;
                Undo.RecordObject(fsm, "Bring Back The Health HUD");
                foreach (var state in fsm.FsmStates) state.SaveActions();
                EditorUtility.SetDirty(fsm);
            }
            return changed;
        }

        // Puts "switch the canvas on" first in the state, unless it already switches it on.
        private static bool SwitchOnFirst(FsmState state, GameObject canvas)
        {
            if (state.Actions.Any(a => Switches(a, canvas, true))) return false;
            var on = new ActivateGameObject();
            on.Reset();
            on.gameObject = new FsmOwnerDefault
            {
                OwnerOption = OwnerDefaultOption.SpecifyGameObject,
                GameObject = new FsmGameObject { Value = canvas },
            };
            on.activate = true;
            state.Actions = new FsmStateAction[] { on }.Concat(state.Actions).ToArray();
            return true;
        }

        // Whether the action switches the canvas on (or off).
        private static bool Switches(FsmStateAction action, GameObject canvas, bool on)
        {
            return action is ActivateGameObject activate && activate.gameObject != null &&
                   activate.gameObject.OwnerOption == OwnerDefaultOption.SpecifyGameObject &&
                   activate.gameObject.GameObject != null && activate.gameObject.GameObject.Value == canvas &&
                   activate.activate != null && activate.activate.Value == on;
        }

        private static Sprite PrepareSprite(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;
            if (importer.textureType != TextureImporterType.Sprite || !importer.mipmapEnabled || importer.maxTextureSize != 256)
            {
                // A small sprite seen from 0.5 m: mipmaps keep it smooth instead of shimmering.
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D PrepareTexture(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return null;
            if (!importer.mipmapEnabled || importer.maxTextureSize != 64 || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 64;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private const string SmokeTexture = "Assets/Lana Studio/Hyper Casual FX/Textures/Dust01.png";
        private const string SmokeMaterialPath = MaterialFolder + "/HUD Smoke.mat";
        private const string SmokeShader = "429/Particles Overlay";

        /// <summary>
        /// Adds the smoke round the heart that shows while a smoked boong's buff is on (the health HUD plays it
        /// and fades it with itself): a "Buff Smoke" particle system under the heart, white cartoon puffs
        /// (Lana's Dust01 sheet) on a see-through overlay material, Assets/Data/UI/HUD Smoke.mat. Safe to run again.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Add Buff Smoke To Health HUD")]
        public static void AddBuffSmoke()
        {
            var hud = Object.FindAnyObjectByType<PlayerHealthHUD>(FindObjectsInactive.Include);
            var heart = hud != null ? new SerializedObject(hud).FindProperty("heart").objectReferenceValue as RectTransform : null;
            var shader = Shader.Find(SmokeShader);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexture);
            if (hud == null || heart == null || shader == null || texture == null)
            {
                Debug.LogError($"[Health HUD] Needs the Health HUD (Set Up Health HUD) with its heart, the shader '{SmokeShader}' " +
                               $"and {SmokeTexture}. Nothing changed.");
                return;
            }

            SetupUtility.EnsureFolder(MaterialFolder);
            var material = GetOrCreateMaterial(SmokeMaterialPath, () => new Material(shader) { name = "HUD Smoke" });
            material.shader = shader;
            material.mainTexture = texture;
            material.renderQueue = 3999;     // over the heart like the sparkles, under the headset fade (4000)
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            var smoke = CreateSmoke(heart, material);
            SetupUtility.SetReference(hud, "buffSmoke", smoke);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            Debug.Log("[Health HUD] The heart now smokes while a boong buff is on ('Buff Smoke' under the heart). Save the scene.", smoke);
        }

        // Cartoon smoke puffs rising from the lower half of the heart and drifting up over it, growing as they go. Sizes
        // and speeds are in canvas units (like the sparkles), in local space so they follow the head.
        private static ParticleSystem CreateSmoke(RectTransform heart, Material material)
        {
            var child = heart.Find("Buff Smoke");
            ParticleSystem ps = child != null ? child.GetComponent<ParticleSystem>() : null;
            if (ps == null)
            {
                var go = new GameObject("Buff Smoke", typeof(ParticleSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create Buff Smoke");
                go.layer = heart.gameObject.layer;
                go.transform.SetParent(heart, false);
                ps = go.GetComponent<ParticleSystem>();
            }
            ps.transform.localPosition = new Vector3(0f, -25f, 0f);
            ps.transform.localRotation = Quaternion.identity;
            ps.transform.localScale = Vector3.one;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(40f, 65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.9f, 0.9f), Color.white);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 10f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(110f, 40f, 10f);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-15f, 15f);
            velocity.y = new ParticleSystem.MinMaxCurve(30f, 60f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.25f)));

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0.3f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-Mathf.PI / 4f, Mathf.PI / 4f);

            // Dust01 is a 2 x 2 sheet of puffs: each particle is one of them, picked at random.
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 3.999f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ps;
        }

        // Little green sparkles rising around the heart while health is above the start. Sizes and speeds are in canvas
        // units (the canvas is scaled to metres), so they stay the same size whatever distance the HUD is placed at.
        // They follow the head (local space). The HUD plays and stops them and fades them with itself.
        private static ParticleSystem CreateSparkles(RectTransform heart, Material material)
        {
            var child = heart.Find("Bonus Sparkles");
            ParticleSystem ps = child != null ? child.GetComponent<ParticleSystem>() : null;
            if (ps == null)
            {
                var go = new GameObject("Bonus Sparkles", typeof(ParticleSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create Bonus Sparkles");
                go.layer = heart.gameObject.layer;
                go.transform.SetParent(heart, false);
                ps = go.GetComponent<ParticleSystem>();
            }
            ps.transform.localPosition = new Vector3(0f, -10f, 0f);
            ps.transform.localRotation = Quaternion.identity;
            ps.transform.localScale = Vector3.one;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(26f, 46f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 1f, 0.45f), new Color(0.8f, 1f, 0.6f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 12f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(170f, 130f, 10f);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-12f, 12f);
            velocity.y = new ParticleSystem.MinMaxCurve(25f, 55f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-Mathf.PI / 2f, Mathf.PI / 2f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return ps;
        }

        private static Material GetOrCreateMaterial(string path, System.Func<Material> make)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = make();
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Image FindOrCreateImage(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null && child.TryGetComponent<Image>(out var image)) return image;
            image = SetupUtility.CreateImage(name, parent, Color.white);
            Undo.RegisterCreatedObjectUndo(image.gameObject, "Create " + name);
            return image;
        }

        private static TextMeshProUGUI FindOrCreateText(Transform parent, string name, string text, float size, TMP_FontAsset font)
        {
            var child = parent.Find(name);
            if (child == null || !child.TryGetComponent<TextMeshProUGUI>(out var label))
            {
                label = SetupUtility.CreateText(name, parent, text, size, TextAlignmentOptions.Left, FontStyles.Bold, font);
                Undo.RegisterCreatedObjectUndo(label.gameObject, "Create " + name);
            }
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
