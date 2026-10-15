using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.VFX;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Set Up Bedroom Shortcut Door. The small bedroom's door (Bedroom_Base_Z > Floor >
    /// Small_Room_Door > Bedroom_Base_Z_Door) becomes a shortcut the player can break:
    /// - it takes hits like StartGamePumpkin (Enemy Hit Damage + a Health FSM, 150 health), wobbles when hurt (a Jiggle
    ///   child holds the door's look), puffs smoke and knocks on wood;
    /// - at 0 it breaks (Door_Break_Drop_Pos drops the pieces) and the lucky coin is tossed (Toss Coin);
    /// - four-leaf clover: GAMESTAGES gets DOOR_SHORTCUT. Its new 'Door Shortcut' state stops the bug waves, cleans the
    ///   room like Clean Mini (smoke + Fairy Shield) and goes straight to Rooftop_ON;
    /// - skull: Pumpkin_1's countdown ends at once and it turns into pumpkin_boss_Z (Hurry Pumpkin Boss). The door goes
    ///   with the small room, so Pumpkin_1 is always still there while the door can be broken.
    /// Needs 2 Game Scene open (for GAMESTAGES). Running it again changes nothing.
    /// </summary>
    public static class ShortcutDoorSetupMenu
    {
        private const string RoomPath = "Assets/Prefabs/Bases/Bedroom_Base_Z.prefab";
        private const string DoorPath = "Bedroom_Base_Z_Armature/Floor/Small_Room_Door/Bedroom_Base_Z_Door";
        private const string StartPumpkinPath = "Assets/Prefabs/Items/Breakable/StartGamePumpkin.prefab";
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        private const string Sfx = "Assets/Audio/SFX/";
        private const string ShortcutEvent = "DOOR_SHORTCUT";
        private const string ShortcutState = "Door Shortcut";
        private const float DoorHealth = 150f;

        [MenuItem("Tools/429 Game/Rooms/Set Up Bedroom Shortcut Door")]
        public static void SetUp()
        {
            var stages = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES");
            if (stages == null)
            {
                Debug.LogError("[Shortcut Door] No GAMESTAGES FSM in the open scene: open 2 Game Scene first. Nothing changed.");
                return;
            }

            var notes = new List<string>();
            var shortcut = FsmEvent.GetFsmEvent(ShortcutEvent);
            shortcut.IsGlobal = true;
            if (!PlayMakerGlobals.Instance.Events.Contains(ShortcutEvent))
            {
                PlayMakerGlobals.Instance.Events.Add(ShortcutEvent);
                EditorUtility.SetDirty(PlayMakerGlobals.Instance);
                AssetDatabase.SaveAssets();
                notes.Add($"global event {ShortcutEvent}");
            }

            if (SetUpDoor(shortcut, notes)) notes.Add("the small bedroom's door breaks (150 health) and tosses the coin");
            if (AddShortcutState(stages, shortcut)) notes.Add($"GAMESTAGES: {ShortcutEvent} > '{ShortcutState}' (waves off, smoke + Fairy Shield) > Rooftop_ON");

            Debug.Log("[Shortcut Door] " + (notes.Count > 0 ? string.Join("; ", notes) : "already set up") + ".");
        }

        [MenuItem("Tools/429 Game/Rooms/Set Up Bedroom Shortcut Door", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        // ---- The door (in the Bedroom_Base_Z prefab)

        private static bool SetUpDoor(FsmEvent shortcut, List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(RoomPath);
            try
            {
                var door = root.transform.Find(DoorPath);
                if (door == null)
                {
                    notes.Add("WARNING: Bedroom_Base_Z has no " + DoorPath + ", so the door wasn't changed");
                    return false;
                }
                if (door.GetComponents<PlayMakerFSM>().Any(f => f.FsmName == "Health")) return false;
                var dropPos = door.Find("Door_Break_Drop_Pos");
                if (dropPos == null)
                {
                    notes.Add("WARNING: the door has no Door_Break_Drop_Pos, so it wasn't changed");
                    return false;
                }

                var jiggle = MoveLookIntoJiggle(door);
                var smoke = AddSmoke(door, jiggle);
                AddHitDamage(door.gameObject);
                AddHealthFsm(door.gameObject, jiggle.gameObject, smoke, dropPos.gameObject, shortcut);

                PrefabUtility.SaveAsPrefabAsset(root, RoomPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The door's look goes on a child of a "Jiggle" pivot at the bottom middle of the door, so it wobbles from its foot
        // while the door itself (its collider, and Close Door turning it) stays put.
        private static Transform MoveLookIntoJiggle(Transform door)
        {
            var filter = door.GetComponent<MeshFilter>();
            var renderer = door.GetComponent<MeshRenderer>();
            var bounds = filter.sharedMesh.bounds;

            var jiggle = new GameObject("Jiggle").transform;
            jiggle.gameObject.layer = door.gameObject.layer;
            jiggle.SetParent(door, false);
            jiggle.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var wobble = jiggle.gameObject.AddComponent<Jiggle>();
            var pumpkinWobble = StartPumpkinPart<Jiggle>("pumpkin_Start/Jiggle");
            if (pumpkinWobble != null) EditorUtility.CopySerialized(pumpkinWobble, wobble);

            var look = new GameObject("Door_Mesh").transform;
            look.gameObject.layer = door.gameObject.layer;
            look.SetParent(jiggle, false);
            look.localPosition = -jiggle.localPosition;
            look.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            EditorUtility.CopySerialized(renderer, look.gameObject.AddComponent<MeshRenderer>());
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(filter);
            return jiggle;
        }

        // A copy of StartGamePumpkin's hit smoke (it switches itself off again), in the middle of the door.
        private static GameObject AddSmoke(Transform door, Transform jiggle)
        {
            var source = StartPumpkinPart<Transform>("pumpkin_Start/Smoke_Blast");
            GameObject smoke;
            if (source != null)
            {
                smoke = Object.Instantiate(source.gameObject, door);
                smoke.name = "Smoke_Blast";
            }
            else
            {
                smoke = new GameObject("Smoke_Blast");
                smoke.transform.SetParent(door, false);
            }
            smoke.transform.localPosition = jiggle.localPosition + Vector3.up * 0.95f;
            smoke.transform.localRotation = Quaternion.identity;
            smoke.SetActive(false);
            return smoke;
        }

        private static void AddHitDamage(GameObject door)
        {
            if (!door.TryGetComponent<EnemyHitDamage>(out var hits)) hits = door.AddComponent<EnemyHitDamage>();
            var so = new SerializedObject(hits);
            so.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath);
            so.FindProperty("toughness").floatValue = 1f;
            so.FindProperty("hitCooldown").floatValue = 0.3f;
            so.FindProperty("knockBack").boolValue = false;
            so.FindProperty("exp").intValue = 0;
            so.FindProperty("healthFsm").stringValue = "Health";
            so.FindProperty("healthVariable").stringValue = "health";
            so.FindProperty("hitEvent").stringValue = "Damage";
            so.FindProperty("weakHitEvent").stringValue = "ShakeSmall";
            // Its other FSM (Close Door) stays on.
            so.FindProperty("oldDamageFsm").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddHealthFsm(GameObject door, GameObject jiggle, GameObject smoke, GameObject dropPos,
            FsmEvent shortcut)
        {
            var component = door.AddComponent<PlayMakerFSM>();
            component.FsmName = "Health";
            var fsm = component.Fsm;

            var health = new FsmFloat("health") { Value = DoorHealth };
            fsm.Variables.FloatVariables = new[] { health };

            var damage = FsmEvent.GetFsmEvent("Damage");
            var shake = FsmEvent.GetFsmEvent("ShakeSmall");
            var broken = FsmEvent.GetFsmEvent("broken");
            var clover = FsmEvent.GetFsmEvent("CLOVER");
            var skull = FsmEvent.GetFsmEvent("SKULL");
            fsm.Events = new[] { damage, shake, broken, clover, skull, FsmEvent.Finished };

            var standing = State(fsm, "Standing", 60, 120,
                new FsmStateAction[] { HealthGone(health, broken, true) },
                (broken, "Break"));
            var hit = State(fsm, "Hit", 300, 40,
                new FsmStateAction[]
                {
                    RandomSound("wood_get_hit_1", "wood_get_hit_2", "sofa_hit_1", "sofa_hit_2"),
                    Activate(smoke, true),
                    // Last, so the killing blow still knocks and puffs first.
                    HealthGone(health, broken, false),
                    Wait(0.25f),
                },
                (broken, "Break"), (FsmEvent.Finished, "Standing"));
            var shaking = State(fsm, "Shake", 300, 200,
                new FsmStateAction[] { JiggleIt(jiggle, 0.3f), Wait(0.2f) },
                (FsmEvent.Finished, "Standing"));
            var breaking = State(fsm, "Break", 540, 120,
                new FsmStateAction[]
                {
                    // No more hits, and nothing in the doorway.
                    // Destroy Component needs the full name of a type in a namespace.
                    RemoveComponent(typeof(EnemyHitDamage).FullName),
                    RemoveComponent("BoxCollider"),
                    Activate(dropPos, true),
                    Activate(jiggle, false),
                    RandomSound("wood_furniture_break_complete", "wood_furniture_break_complete_2"),
                    // The pieces fly for a moment before the game stops for the coin.
                    Wait(0.8f),
                },
                (FsmEvent.Finished, "Toss Coin"));
            var toss = State(fsm, "Toss Coin", 760, 120,
                new FsmStateAction[] { new TossCoin { clover = clover, skull = skull } },
                (clover, "Shortcut"), (skull, "Boss"));
            var toRooftop = State(fsm, "Shortcut", 980, 40,
                new FsmStateAction[] { SendToGameStages(shortcut) },
                (FsmEvent.Finished, "Gone"));
            var boss = State(fsm, "Boss", 980, 200,
                new FsmStateAction[]
                {
                    new HurryPumpkinBoss
                    {
                        pumpkinTag = "Pumpkin_1",
                        countdownFsm = "Wait and Transform",
                        countdownState = "Wait 360s",
                    },
                },
                (FsmEvent.Finished, "Gone"));
            var gone = State(fsm, "Gone", 1200, 120,
                new FsmStateAction[] { new DestroySelf { delay = 0f, realTime = false, detachChildren = false } });

            fsm.States = new[] { standing, hit, shaking, breaking, toss, toRooftop, boss, gone };
            fsm.StartState = standing.Name;
            fsm.GlobalTransitions = new[]
            {
                new FsmTransition { FsmEvent = damage, ToState = hit.Name },
                new FsmTransition { FsmEvent = shake, ToState = shaking.Name },
            };
            foreach (var s in fsm.States) s.SaveActions();
            EditorUtility.SetDirty(component);
        }

        // ---- GAMESTAGES (in the scene)

        private static bool AddShortcutState(PlayMakerFSM stages, FsmEvent shortcut)
        {
            var fsm = stages.Fsm;
            if (!fsm.Initialized) fsm.Init(stages);
            foreach (var s in fsm.States) s.LoadActions();
            if (fsm.States.Any(s => s.Name == ShortcutState)) return false;

            var cleanMini = fsm.States.FirstOrDefault(s => s.Name == "Clean Mini");
            var rooftop = fsm.States.FirstOrDefault(s => s.Name == "Rooftop_ON");
            if (cleanMini == null || rooftop == null)
            {
                Debug.LogWarning("[Shortcut Door] GAMESTAGES has no 'Clean Mini' or 'Rooftop_ON' state, so the shortcut wasn't added there.");
                return false;
            }

            var actions = new List<FsmStateAction>();
            // The bug waves stop (their bugs are popped by the shield below).
            var managers = stages.transform.parent;
            for (int i = 0; i < 4; i++)
            {
                var manager = managers != null ? managers.Find("-EnemyManager " + i) : null;
                if (manager != null) actions.Add(Activate(manager.gameObject, false));
            }
            // The same clean-up as Clean Mini: the brown smoke and the Fairy Shield.
            foreach (var a in cleanMini.Actions.OfType<ActivateGameObject>())
            {
                actions.Add(new ActivateGameObject
                {
                    gameObject = a.gameObject,
                    activate = a.activate.Value,
                    recursive = a.recursive.Value,
                    resetOnExit = a.resetOnExit,
                    everyFrame = false,
                });
            }
            actions.Add(Wait(3f));

            var state = new FsmState(fsm)
            {
                Name = ShortcutState,
                Position = new Rect(rooftop.Position.x - 260f, rooftop.Position.y + 70f, 140f, 32f),
                Actions = actions.ToArray(),
                Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = rooftop.Name } },
            };
            fsm.States = fsm.States.Append(state).ToArray();
            if (!fsm.Events.Contains(shortcut)) fsm.Events = fsm.Events.Append(shortcut).ToArray();
            fsm.GlobalTransitions = fsm.GlobalTransitions
                .Append(new FsmTransition { FsmEvent = shortcut, ToState = state.Name }).ToArray();
            state.SaveActions();

            EditorUtility.SetDirty(stages);
            EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
            EditorSceneManager.SaveScene(stages.gameObject.scene);
            return true;
        }

        // ---- Building blocks

        private static FsmState State(Fsm fsm, string name, float x, float y, FsmStateAction[] actions,
            params (FsmEvent ev, string to)[] transitions)
        {
            return new FsmState(fsm)
            {
                Name = name,
                Position = new Rect(x, y, 120f, 32f),
                Actions = actions,
                Transitions = transitions.Select(t => new FsmTransition { FsmEvent = t.ev, ToState = t.to }).ToArray(),
            };
        }

        // Health at 0 or below sends 'broken' (checked every frame, or once).
        private static FloatCompare HealthGone(FsmFloat health, FsmEvent broken, bool everyFrame)
        {
            var compare = new FloatCompare();
            compare.Reset();
            compare.float1 = health;
            compare.float2 = 0f;
            compare.equal = broken;
            compare.lessThan = broken;
            compare.everyFrame = everyFrame;
            return compare;
        }

        private static Wait Wait(float seconds)
        {
            var wait = new Wait();
            wait.Reset();
            wait.time = seconds;
            wait.finishEvent = FsmEvent.Finished;
            return wait;
        }

        private static ActivateGameObject Activate(GameObject target, bool on)
        {
            var activate = new ActivateGameObject();
            activate.Reset();
            activate.gameObject = Specify(target);
            activate.activate = on;
            activate.recursive = false;
            return activate;
        }

        private static JiggleObject JiggleIt(GameObject target, float strength)
        {
            var jiggle = new JiggleObject();
            jiggle.Reset();
            jiggle.gameObject = Specify(target);
            jiggle.strength = strength;
            return jiggle;
        }

        private static DestroyComponent RemoveComponent(string type)
        {
            var remove = new DestroyComponent();
            remove.Reset();
            remove.gameObject = new FsmOwnerDefault();
            remove.component = type;
            return remove;
        }

        // One of these clips, played at the door.
        private static PlayRandomSound RandomSound(params string[] clips)
        {
            var sound = new PlayRandomSound();
            sound.Reset();
            sound.gameObject = new FsmOwnerDefault();
            var found = clips.Select(c => AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + c + ".wav")).Where(c => c != null).ToArray();
            sound.audioClips = found.Select(c => new FsmObject { ObjectType = typeof(AudioClip), Value = c }).ToArray();
            sound.weights = found.Select(_ => new FsmFloat { Value = 1f }).ToArray();
            sound.volume = 1f;
            sound.noRepeat = true;
            return sound;
        }

        // DOOR_SHORTCUT to every FSM: only GAMESTAGES has a transition for it.
        private static SendEvent SendToGameStages(FsmEvent shortcut)
        {
            var send = new SendEvent();
            send.Reset();
            send.eventTarget = new FsmEventTarget
            {
                target = FsmEventTarget.EventTarget.BroadcastAll,
                excludeSelf = false,
                gameObject = new FsmOwnerDefault(),
                fsmName = "",
                sendToChildren = false,
            };
            send.sendEvent = shortcut;
            send.delay = 0f;
            return send;
        }

        private static FsmOwnerDefault Specify(GameObject target) => new()
        {
            OwnerOption = OwnerDefaultOption.SpecifyGameObject,
            GameObject = new FsmGameObject { Value = target },
        };

        private static T StartPumpkinPart<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartPumpkinPath);
            var part = prefab != null ? prefab.transform.Find(path) : null;
            return part != null ? part.GetComponent<T>() : null;
        }
    }
}
