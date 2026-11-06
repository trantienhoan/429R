using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Bonus > Set Up Thank You Reward (user, 2026-11-06). Safe to run again.
    /// - Treasure_Chest_GAMEOVER (Items/Chest): its Open_Chest state gets Play End Game Jackpot (Firework_Big, the miracle
    ///   sound, 3 s of gold EXP orbs, 500 EXP).
    /// - GAMESTAGES 'Thank For Playing' (open scene): the Fairy's green good light (Spot Light_2 with its glow, circles and
    ///   stars, left behind when she flies off) is switched off instead of being scaled up x3 (user: "that green light is
    ///   really annoying"). SAVE the scene afterwards.
    /// </summary>
    public static class ThankYouRewardSetupMenu
    {
        private const string ChestPath = "Assets/Prefabs/Items/Chest/Treasure_Chest_GAMEOVER.prefab";
        private const string Sound = "Assets/Audio/SFX/A_gentle_miracle_sound_1.wav";
        private const string Firework = "Assets/Prefabs/Light/Firework_Big.prefab";
        private const string GreenLight = "Fairy_Good_Light";

        [MenuItem("Tools/429 Game/Bonus/Set Up Thank You Reward")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Thank You Reward] Stop play mode first. Nothing changed."); return; }
            var notes = Run();
            Debug.Log("[Thank You Reward] " + string.Join("\n", notes));
            EditorUtility.DisplayDialog("Set Up Thank You Reward", string.Join("\n", notes), "OK");
        }

        public static List<string> Run()
        {
            var notes = new List<string>();
            AddJackpot(notes);
            GreenLightOff(notes);
            return notes;
        }

        private static void AddJackpot(List<string> notes)
        {
            var root = PrefabUtility.LoadPrefabContents(ChestPath);
            try
            {
                var fsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Chest_Control");
                if (fsm == null) { notes.Add("No Chest_Control FSM on " + ChestPath); return; }
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var open = fsm.Fsm.GetState("Open_Chest");
                if (open == null) { notes.Add("No Open_Chest state"); return; }
                open.LoadActions();
                var jackpot = open.Actions.OfType<PlayEndGameJackpot>().FirstOrDefault();
                if (jackpot == null)
                {
                    jackpot = new PlayEndGameJackpot();
                    jackpot.Reset();
                    jackpot.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner };
                    jackpot.sound = new FsmObject { Value = AssetDatabase.LoadAssetAtPath<AudioClip>(Sound) };
                    jackpot.effects = new[] { new FsmGameObject { Value = AssetDatabase.LoadAssetAtPath<GameObject>(Firework) } };
                    open.Actions = open.Actions.Append(jackpot).ToArray();
                    open.SaveActions();
                    EditorUtility.SetDirty(fsm);
                    PrefabUtility.SaveAsPrefabAsset(root, ChestPath);
                    notes.Add("Treasure_Chest_GAMEOVER: Open_Chest plays the End Game Jackpot (" + jackpot.exp.Value + " EXP in gold orbs over " + jackpot.seconds.Value + " s, Firework_Big, miracle sound).");
                }
                else notes.Add("Treasure_Chest_GAMEOVER: already has the jackpot (" + jackpot.exp.Value + " EXP).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void GreenLightOff(List<string> notes)
        {
            var gs = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "GAMESTAGES" && f.gameObject.scene.IsValid());
            if (gs == null) { notes.Add("No GAMESTAGES in the open scene: open 2 Game Scene for the green light."); return; }
            var f = gs.Fsm;
            if (!f.Initialized) f.Init(gs);
            var state = f.GetState("Thank For Playing");
            if (state == null) { notes.Add("GAMESTAGES has no 'Thank For Playing'."); return; }
            Undo.RecordObject(gs, "Thank You Reward");
            state.LoadActions();
            bool changed = false;
            foreach (var action in state.Actions)
            {
                if (action is TweenScale scale && Targets(scale.gameObject) && action.Enabled) { action.Enabled = false; changed = true; }
            }
            if (!state.Actions.OfType<ActivateGameObject>().Any(a => Targets(a.gameObject) && !a.activate.Value))
            {
                var off = new ActivateGameObject();
                off.Reset();
                off.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject(GreenLight) { UseVariable = true } };
                off.activate = false;
                state.Actions = new FsmStateAction[] { off }.Concat(state.Actions).ToArray();
                changed = true;
            }
            if (changed)
            {
                state.SaveActions();
                EditorUtility.SetDirty(gs);
                EditorSceneManager.MarkSceneDirty(gs.gameObject.scene);
                notes.Add("GAMESTAGES 'Thank For Playing': the Fairy's green light is switched off (its x3 scale-up is off). SAVE the scene.");
            }
            else notes.Add("GAMESTAGES 'Thank For Playing': the green light is already off.");
        }

        private static bool Targets(FsmOwnerDefault target) =>
            target != null && target.OwnerOption == OwnerDefaultOption.SpecifyGameObject && target.GameObject != null &&
            target.GameObject.UseVariable && target.GameObject.Name == GreenLight;
    }
}
