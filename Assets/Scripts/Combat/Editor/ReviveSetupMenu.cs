using System.Linq;
using Game.Combat;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Menu command that sets up getting back up after dying (Tools > 429 Game > Player).</summary>
    public static class ReviveSetupMenu
    {
        private const string Revive = "Revive";

        /// <summary>
        /// Puts Player Revive on the XR Origin and makes its PlayerDeath FSM start with a choice: "Revive?" (Try Revive)
        /// uses up one of the run's revives and goes to "Not Yet" (Revive Player: "Not yet", the fall backwards, full
        /// health, the wake-up aura), then "Revived" switches PlayerDeath off again; with no revive left it goes on to
        /// "Dying..." as before (the game over). Also makes a hit while the player is down leave PlayerDeath running
        /// (PlayerHealth's DEAD doesn't switch it off on the way out). Running it again changes nothing.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Set Up Revive")]
        public static void SetUp()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            var death = origin != null ? origin.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "PlayerDeath") : null;
            if (death == null)
            {
                Debug.LogError("[Revive] Needs the XR Origin with its PlayerDeath FSM in the open scene. Nothing changed.");
                return;
            }

            string reviveNote = "the XR Origin already has Player Revive";
            if (!origin.TryGetComponent<PlayerRevive>(out _))
            {
                Undo.AddComponent<PlayerRevive>(origin.gameObject);
                reviveNote = "added Player Revive to the XR Origin (drop the \"Not yet\" sound into its Not Yet Sound)";
            }

            string fsmNote;
            var f = death.Fsm;
            if (!f.Initialized) f.Init(death);
            foreach (var s in f.States) s.LoadActions();
            if (f.States.Any(s => s.Name == "Revive?"))
            {
                fsmNote = "PlayerDeath already starts with Revive?";
            }
            else
            {
                Undo.RecordObject(death, "Set Up Revive");
                string dying = f.StartState;
                var near = f.States.First(s => s.Name == dying).Position;
                var reviveEvent = FsmEvent.GetFsmEvent(Revive);
                if (!f.Events.Contains(reviveEvent)) f.Events = f.Events.Append(reviveEvent).ToArray();

                var choose = NewState(f, "Revive?", near, -260f, 0f);
                var tryRevive = New<TryRevive>();
                tryRevive.revive = reviveEvent;
                tryRevive.gameOver = FsmEvent.Finished;
                choose.Actions = new FsmStateAction[] { tryRevive };
                choose.Transitions = new[] { Go(Revive, "Not Yet"), Go("FINISHED", dying) };

                var getUp = NewState(f, "Not Yet", near, -260f, 120f);
                getUp.Actions = new FsmStateAction[] { New<RevivePlayer>() };
                getUp.Transitions = new[] { Go("FINISHED", "Revived") };

                // As DEAD does at the end: PlayerDeath off again, so the next death starts it over.
                var done = NewState(f, "Revived", near, -260f, 240f);
                var off = New<EnableFSM>();
                off.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner };
                off.fsmName = "PlayerDeath";
                off.enable = false;
                off.resetOnExit = false;
                done.Actions = new FsmStateAction[] { off };
                done.Transitions = new FsmTransition[0];

                f.StartState = "Revive?";
                foreach (var s in f.States) s.SaveActions();
                EditorUtility.SetDirty(death);
                fsmNote = $"PlayerDeath now starts with Revive? (a revive left: Not Yet, Revived; none: {dying})";
            }

            string healthNote = KeepDeathRunning(origin);

            EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            Selection.activeGameObject = origin.gameObject;
            Debug.Log($"[Revive] {reviveNote}; {fsmNote}; {healthNote}. Save the scene.", origin);
        }

        // A hit while the player is down (the boss, a bug pumpkin, anything sending straight to PlayerHealth) takes
        // PlayerHealth out of DEAD and back in. DEAD's Enable FSM switched PlayerDeath off on the way out, so it started
        // over: the coin toss was thrown away with its coin already spent, and with no coin left that was a game over at
        // once. PlayerDeath now runs on (it switches itself off when it's done, in Revived and DEAD).
        private static string KeepDeathRunning(XROrigin origin)
        {
            var health = origin.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "PlayerHealth");
            if (health == null) return "WARNING: no PlayerHealth FSM on the XR Origin";
            var f = health.Fsm;
            if (!f.Initialized) f.Init(health);
            foreach (var s in f.States) s.LoadActions();
            var dead = f.States.FirstOrDefault(s => s.Name == "DEAD");
            var enable = dead?.Actions.OfType<EnableFSM>().FirstOrDefault(a => a.enable.Value && a.fsmName.Value == "PlayerDeath");
            if (enable == null) return "WARNING: PlayerHealth has no DEAD state switching PlayerDeath on";
            if (dead.ActionData.ActionCount != dead.Actions.Length) return "WARNING: PlayerHealth's DEAD didn't load all its actions, left alone";
            if (!enable.resetOnExit.Value) return "PlayerHealth's DEAD already leaves PlayerDeath running";
            Undo.RecordObject(health, "Set Up Revive");
            enable.resetOnExit = false;
            dead.SaveActions();
            EditorUtility.SetDirty(health);
            return "PlayerHealth's DEAD no longer switches PlayerDeath off when a hit comes in while the player is down";
        }

        [MenuItem("Tools/429 Game/Player/Set Up Revive", true)]
        private static bool CanSetUp() => !EditorApplication.isPlaying;

        private const string CoinFolder = "Assets/Prefabs/Items/Revive_Coin";
        private const string StandInCoin = CoinFolder + "/Revive_Coin_StandIn.prefab";
        private const string Sfx = "Assets/Audio/SFX/";

        /// <summary>
        /// Puts Revive Coins on the XR Origin (next to Player Revive), so the revives left are lucky coins flipped when
        /// the player goes down. Empty slots get the stand-in coin (made from coin_clover.png and coin_skull.png in
        /// Prefabs/Items/Revive_Coin if it isn't there yet) and the game's sounds; whatever is already set stays.
        /// </summary>
        [MenuItem("Tools/429 Game/Player/Set Up Revive Coins")]
        public static void SetUpCoins()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
            if (origin == null || !origin.TryGetComponent<PlayerRevive>(out _))
            {
                Debug.LogError("[Revive Coins] Needs the XR Origin with Player Revive in the open scene (Tools > 429 Game > Player > Set Up Revive first). Nothing changed.");
                return;
            }

            var coin = AssetDatabase.LoadAssetAtPath<GameObject>(StandInCoin);
            string coinNote = "";
            if (coin == null)
            {
                coin = MakeStandInCoin();
                coinNote = coin != null ? "made the stand-in coin; " : "couldn't make the stand-in coin (coin_clover.png / coin_skull.png missing); ";
            }

            bool added = !origin.TryGetComponent<ReviveCoins>(out var coins);
            if (added) coins = Undo.AddComponent<ReviveCoins>(origin.gameObject);
            var so = new SerializedObject(coins);
            int filled = 0;
            filled += Fill(so, "coinPrefab", coin);
            filled += Fill(so, "tossSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "whoosh.wav"));
            filled += Fill(so, "cloverSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "shop_buy_success.wav"));
            filled += Fill(so, "skullSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "Low,_rumbling_hmmmm.wav"));
            filled += Fill(so, "winSound", AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + "health_sfx.wav"));
            var land = so.FindProperty("landSounds");
            if (land.arraySize == 0)
            {
                var clips = new[] { "metal_object_hit_1.wav", "metal_object_hit_2.wav", "metal_object_hit_3.wav" }
                    .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + n)).Where(c => c != null).ToArray();
                land.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++) land.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                if (clips.Length > 0) filled++;
            }
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(origin.gameObject.scene);
            Selection.activeGameObject = origin.gameObject;
            Debug.Log($"[Revive Coins] {coinNote}{(added ? "added Revive Coins to the XR Origin" : "the XR Origin already has Revive Coins")}" +
                      $"{(filled > 0 ? $", filled {filled} empty slot(s)" : "")}. Save the scene.", origin);
        }

        [MenuItem("Tools/429 Game/Player/Set Up Revive Coins", true)]
        private static bool CanSetUpCoins() => !EditorApplication.isPlaying;

        private static int Fill(SerializedObject so, string property, Object value)
        {
            var p = so.FindProperty(property);
            if (p == null || p.objectReferenceValue != null || value == null) return 0;
            p.objectReferenceValue = value;
            return 1;
        }

        // A gold coin with the clover on its top (Y) and the skull underneath, both pictures with their top towards Z.
        private static GameObject MakeStandInCoin()
        {
            var clover = Face("coin_clover");
            var skull = Face("coin_skull");
            if (clover == null || skull == null) return null;

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var gold = new Material(lit) { name = "Revive_Coin_Gold" };
            gold.SetColor("_BaseColor", new Color(1f, 0.76f, 0.28f, 1f));
            gold.SetFloat("_Metallic", 0.8f);
            gold.SetFloat("_Smoothness", 0.65f);
            Glow(gold, new Color(0.3f, 0.2f, 0.05f, 1f), null);
            AssetDatabase.CreateAsset(gold, CoinFolder + "/Revive_Coin_Gold.mat");

            var root = new GameObject("Revive_Coin_StandIn");
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "Body";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = new Vector3(1f, 0.06f, 1f);
                body.GetComponent<MeshRenderer>().sharedMaterial = gold;

                FaceQuad(root.transform, "Clover", clover, 1f);
                FaceQuad(root.transform, "Skull", skull, -1f);
                return PrefabUtility.SaveAsPrefabAsset(root, StandInCoin);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The face picture as a cut-out, lit, a little glowing material.
        private static Material Face(string picture)
        {
            string path = $"{CoinFolder}/{picture}.png";
            // A picture just put in the folder isn't imported yet.
            if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) return null;

            var face = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Revive_" + picture };
            face.SetTexture("_BaseMap", texture);
            face.SetColor("_BaseColor", Color.white);
            face.SetFloat("_Metallic", 0.45f);
            face.SetFloat("_Smoothness", 0.6f);
            face.SetFloat("_AlphaClip", 1f);
            face.SetFloat("_Cutoff", 0.5f);
            face.EnableKeyword("_ALPHATEST_ON");
            face.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            Glow(face, new Color(0.4f, 0.4f, 0.4f, 1f), texture);
            AssetDatabase.CreateAsset(face, $"{CoinFolder}/Revive_{picture}.mat");
            return face;
        }

        // A little self-light, so the coin shows in the dark.
        private static void Glow(Material material, Color color, Texture texture)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
            if (texture != null) material.SetTexture("_EmissionMap", texture);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        // A quad just off the coin's top (up = 1) or bottom (up = -1), facing out, its picture's top towards Z.
        private static void FaceQuad(Transform coin, string name, Material material, float up)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(coin, false);
            quad.transform.localPosition = new Vector3(0f, 0.0605f * up, 0f);
            // A quad shows towards its -Z: point its Z into the coin.
            quad.transform.localRotation = Quaternion.LookRotation(up > 0f ? Vector3.down : Vector3.up, Vector3.forward);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static FsmState NewState(Fsm f, string name, Rect near, float x, float y)
        {
            var state = new FsmState(f) { Name = name, Position = new Rect(near.x + x, near.y + y, near.width, near.height) };
            f.States = f.States.Append(state).ToArray();
            return state;
        }

        private static T New<T>() where T : FsmStateAction, new()
        {
            var action = new T();
            action.Reset();
            return action;
        }

        private static FsmTransition Go(string eventName, string toState) =>
            new() { FsmEvent = FsmEvent.GetFsmEvent(eventName), ToState = toState };
    }
}
