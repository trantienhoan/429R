using System.Collections.Generic;
using System.Linq;
using Game.VFX;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Enemies > Whack Pumpkins: Jiggle + Tougher Whack 7 (user, 2026-10-30: "make Pumpkin in Whack 7
    /// faster or tankier than whack 6", picked faster + 2 hits, "can we add jiggle to this pumpkin 6 7 also?").
    /// - Both barrel pumpkins (Pumpkin_Mob_Pumpkin_6/7): Part Jiggle on their pumpkin head (the bone with their hit
    ///   sphere), squashing and rocking around its middle after the animation, on every hit of the player's.
    /// - Whack 7's pumpkin takes two hits: the first makes it flinch (its ouch, and the jiggle) and it keeps taunting; the
    ///   second knocks it out as before. A blast counts as one hit. Its animations play Whack 7 Speed times as fast, so it
    ///   rises, taunts and ducks back in about 4.3 s instead of 6.5 s; its sounds play as fast too (its Audio Source's
    ///   pitch), so the 6.5 s taunt (Pumpkin_leuleu1) still ends as it ducks (user: "the sound is not sync").
    /// - Whack 7's game (GAME-Pumpkin7 in the open scene): the pumpkin stays up 4.3 s (was 6.4), the pause before the
    ///   next one is 0.4 s (was 0.6) and its barrel shakes 0.4 s as a warning (was 0.6). The scene is marked changed,
    ///   not saved.
    /// Running it again changes nothing more.
    /// </summary>
    public static class WhackPumpkinSetupMenu
    {
        private const string MobPath = "Assets/Prefabs/Enemies/Pumpkin_Mob_Pumpkin_{0}.prefab";
        private const string HeadBone = "head";
        private const string Whack7Holder = "-Wood_Barrels_Whack_Pumpkin_7-";
        private const string Whack7Fsm = "GAME-Pumpkin7";

        private const int Whack7Hits = 2;
        private const float Whack7Speed = 1.5f;
        private const float FlinchSeconds = 0.25f;
        private const float UpSeconds = 4.3f;
        private const float PauseSeconds = 0.4f;
        // The barrel's two shakes (the second one ends the warning).
        private const float ShakeSeconds = 0.4f;
        private const float FirstShakeSeconds = 0.35f;

        private const string HitsVariable = "hitsLeft";
        private const string CountState = "Count Hit";
        private const string FlinchState = "Flinch";

        [MenuItem("Tools/429 Game/Enemies/Whack Pumpkins: Jiggle + Tougher Whack 7")]
        public static void Run()
        {
            var notes = new List<string>();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            foreach (var n in new[] { 6, 7 })
            {
                string path = string.Format(MobPath, n);
                if (stage != null && stage.assetPath == path) { notes.Add($"Pumpkin_Mob_Pumpkin_{n} is open in Prefab Mode: skipped"); continue; }
                notes.Add(SetUpMob(path, n == 7));
            }
            notes.Add(FasterWhack7());
            Debug.Log("[Whack Pumpkins] " + string.Join("; ", notes));
        }

        [MenuItem("Tools/429 Game/Enemies/Whack Pumpkins: Jiggle + Tougher Whack 7", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static string SetUpMob(string path, bool whack7)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var done = new List<string>();
                if (AddJiggle(root)) done.Add("head jiggles");
                if (whack7)
                {
                    var damage = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Damage");
                    string tough = damage != null ? TwoHits(damage) : "no Damage FSM";
                    if (tough != null) done.Add(tough);
                    if (root.TryGetComponent(out AudioSource voice) && !Mathf.Approximately(voice.pitch, Whack7Speed))
                    {
                        voice.pitch = Whack7Speed;
                        done.Add($"sounds x{Whack7Speed}");
                    }
                }
                if (done.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                return root.name + ": " + (done.Count > 0 ? string.Join(", ", done) : "already set up");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Part Jiggle on the pumpkin head, around the middle of the mesh it carries (in the head bone's own space).
        private static bool AddJiggle(GameObject root)
        {
            if (root.TryGetComponent(out PartJiggle _)) return false;
            var skin = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            int head = skin != null ? System.Array.FindIndex(skin.bones, b => b != null && b.name == HeadBone) : -1;
            if (head < 0) return false;
            var mesh = skin.sharedMesh;
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var toHead = mesh.bindposes[head];
            var middle = Vector3.zero;
            int count = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (weights[i].boneIndex0 != head || weights[i].weight0 < 0.7f) continue;
                middle += toHead.MultiplyPoint3x4(vertices[i]);
                count++;
            }
            if (count > 0) middle /= count;

            var jiggle = root.AddComponent<PartJiggle>();
            var settings = new SerializedObject(jiggle);
            var parts = settings.FindProperty("parts");
            parts.arraySize = 1;
            parts.GetArrayElementAtIndex(0).FindPropertyRelative("bone").stringValue = HeadBone;
            parts.GetArrayElementAtIndex(0).FindPropertyRelative("centre").vector3Value = middle;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // Check Force's "next" (and every Blast) goes to Count Hit: one off Hits Left; none left, it's knocked out (Deal 1
        // damage); otherwise it flinches (its ouch) and waits for the next hit. Its animations play faster from the start.
        // Null if it was already done.
        private static string TwoHits(PlayMakerFSM damage)
        {
            var fsm = damage.Fsm;
            if (!fsm.Initialized) fsm.Init(damage);
            foreach (var s in fsm.States) s.LoadActions();
            if (fsm.States.Any(s => s.Name == CountState)) return null;
            if (fsm.States.Any(s => s.ActionData.ActionCount != s.Actions.Length)) return "an action in its Damage FSM couldn't be read: no 2 hits";

            var check = fsm.GetState("Check force");
            var deal = fsm.GetState("Deal 1 damage");
            var waiting = fsm.States.FirstOrDefault(s => string.Equals(s.Name, "Wait for Collision", System.StringComparison.OrdinalIgnoreCase));
            var start = fsm.GetState(fsm.StartState);
            var next = FsmEvent.GetFsmEvent("next");
            var back = FsmEvent.GetFsmEvent("back");
            var blast = FsmEvent.GetFsmEvent("Blast");
            if (check == null || deal == null || waiting == null || start == null) return "its Damage FSM isn't laid out as expected: no 2 hits";
            foreach (var e in new[] { next, back, blast })
            {
                if (!fsm.Events.Contains(e)) fsm.Events = fsm.Events.Append(e).ToArray();
            }

            var hits = new FsmInt(HitsVariable) { Value = Whack7Hits };
            fsm.Variables.IntVariables = fsm.Variables.IntVariables.Append(hits).ToArray();
            hits = fsm.Variables.GetFsmInt(HitsVariable);

            var less = new IntAdd();
            less.Reset();
            less.intVariable = hits;
            less.add = -1;
            var left = new IntCompare();
            left.Reset();
            left.integer1 = hits;
            left.integer2 = 0;
            left.equal = next;
            left.lessThan = next;
            left.greaterThan = back;
            var count = new FsmState(fsm)
            {
                Name = CountState,
                Position = new Rect(check.Position.x + 190f, check.Position.y, 100f, 32f),
                Actions = new FsmStateAction[] { less, left },
                Transitions = new[]
                {
                    new FsmTransition { FsmEvent = next, ToState = deal.Name },
                    new FsmTransition { FsmEvent = back, ToState = FlinchState },
                },
            };

            // The ouch, as Deal 1 damage picks and plays it, without waiting for it to end.
            var flinchActions = new List<FsmStateAction>();
            var pickOuch = deal.Actions.OfType<ArrayGetRandom>().FirstOrDefault();
            var playOuch = deal.Actions.OfType<AudioPlay>().FirstOrDefault();
            if (pickOuch != null && playOuch != null)
            {
                var pick = new ArrayGetRandom();
                pick.Reset();
                pick.array = pickOuch.array;
                pick.storeValue = pickOuch.storeValue;
                pick.index = pickOuch.index;
                pick.noRepeat = pickOuch.noRepeat;
                var play = new AudioPlay();
                play.Reset();
                play.gameObject = new FsmOwnerDefault();
                play.volume = playOuch.volume;
                play.oneShotClip = playOuch.oneShotClip;
                play.WaitForEndOfClip = false;
                flinchActions.Add(pick);
                flinchActions.Add(play);
            }
            var wait = new Wait();
            wait.Reset();
            wait.time = FlinchSeconds;
            wait.finishEvent = FsmEvent.Finished;
            flinchActions.Add(wait);
            var flinch = new FsmState(fsm)
            {
                Name = FlinchState,
                Position = new Rect(check.Position.x + 190f, check.Position.y + 90f, 100f, 48f),
                Actions = flinchActions.ToArray(),
                Transitions = new[]
                {
                    new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = waiting.Name },
                    new FsmTransition { FsmEvent = blast, ToState = CountState },
                },
            };
            fsm.States = fsm.States.Append(count).Append(flinch).ToArray();

            foreach (var transition in check.Transitions.Where(t => t.EventName == next.Name)) transition.ToState = CountState;
            foreach (var state in fsm.States)
            {
                foreach (var transition in state.Transitions.Where(t => t.EventName == blast.Name && t.ToState == deal.Name)) transition.ToState = CountState;
            }

            // Its animations (rise, taunt, duck, the knock-out) Whack 7 Speed times as fast, from the moment it appears.
            var faster = new SetAnimatorSpeed();
            faster.Reset();
            faster.gameObject = new FsmOwnerDefault();
            faster.speed = Whack7Speed;
            start.Actions = new FsmStateAction[] { faster }.Concat(start.Actions).ToArray();

            foreach (var s in fsm.States) s.SaveActions();
            EditorUtility.SetDirty(damage);
            return $"takes {Whack7Hits} hits (flinches at the first), animations x{Whack7Speed}";
        }

        // GAME-Pumpkin7 in the open scene: the pumpkin stays up shorter, comes sooner, with a shorter warning shake.
        private static string FasterWhack7()
        {
            var scene = SceneManager.GetActiveScene();
            var holder = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == Whack7Holder);
            var fsm = holder != null ? holder.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == Whack7Fsm) : null;
            if (fsm == null) return $"{Whack7Holder} / {Whack7Fsm} not in the open scene: timings unchanged";
            var f = fsm.Fsm;
            if (!f.Initialized) f.Init(fsm);
            var states = new[] { "Wait", "Shake", "Play Leuleu" }.Select(f.GetState).ToArray();
            foreach (var s in states) s?.LoadActions();
            if (states.Any(s => s == null || s.ActionData.ActionCount != s.Actions.Length))
                return $"{Whack7Fsm}'s Wait, Shake or Play Leuleu is missing or couldn't be read: timings unchanged";

            var pause = states[0].Actions.OfType<Wait>().FirstOrDefault();
            var shakes = states[1].Actions.OfType<TweenPunch>().ToList();
            var up = states[2].Actions.OfType<Wait>().FirstOrDefault();
            if (pause == null || shakes.Count < 2 || up == null) return $"{Whack7Fsm} isn't laid out as expected: timings unchanged";
            if (Mathf.Approximately(pause.time.Value, PauseSeconds) && Mathf.Approximately(up.time.Value, UpSeconds) &&
                Mathf.Approximately(shakes[1].time.Value, ShakeSeconds)) return $"{Whack7Fsm}: timings already set";

            Undo.RecordObject(fsm, "Faster Whack 7");
            pause.time = PauseSeconds;
            shakes[0].time = FirstShakeSeconds;
            shakes[1].time = ShakeSeconds;
            up.time = UpSeconds;
            foreach (var s in states) s.SaveActions();
            EditorUtility.SetDirty(fsm);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"{Whack7Fsm}: up {UpSeconds} s, pause {PauseSeconds} s, shake {ShakeSeconds} s (save the scene)";
        }
    }
}
