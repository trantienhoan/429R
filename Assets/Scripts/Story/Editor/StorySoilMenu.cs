using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Story > Set Up Seeds Into Soil (user's story, 2026-11-23, part 3: "all the green pieces I
    /// collected start flying out and join in together and dive down under the dirt space ... a big tree starts growing
    /// up"). The rooftop's Pumpkin_Tree_Pos FSM waits (3 s) after the lid breaks, then makes the tree; this puts a Seeds
    /// Into Soil action first in that wait (state Init): the pieces fly from the player to the spot, join and dive in, a
    /// quarter second before the tree comes. Pieces look like Seed_Bracer, the puff is Smoke_Flash_Small_Puff.
    /// Running it again changes nothing more. Prefab only (Rooftop_Base_Z): no scene change.
    /// </summary>
    public static class StorySoilMenu
    {
        private const string Tag = "[Seeds Into Soil]";
        private const string RooftopPath = "Assets/Prefabs/Bases/Rooftop_Base_Z.prefab";
        private const string PiecePath = "Assets/Prefabs/Items/Chest/Seed_Bracer.prefab";
        private const string PuffPath = "Assets/Prefabs/Effects/Smoke/Smoke_Flash_Small_Puff.prefab";
        private const string SpotName = "Pumpkin_Tree_Pos";

        [MenuItem("Tools/429 Game/Story/Set Up Seeds Into Soil")]
        public static void Run()
        {
            var piece = AssetDatabase.LoadAssetAtPath<GameObject>(PiecePath);
            var puff = AssetDatabase.LoadAssetAtPath<GameObject>(PuffPath);
            if (piece == null)
            {
                Debug.LogError(Tag + " No " + PiecePath + ".");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(RooftopPath);
            try
            {
                var spot = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == SpotName);
                var fsm = spot != null ? spot.GetComponents<PlayMakerFSM>().FirstOrDefault() : null;
                if (fsm == null)
                {
                    Debug.LogError(Tag + " No FSM on " + SpotName + " in Rooftop_Base_Z.");
                    return;
                }
                if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                var init = fsm.Fsm.GetState("Init");
                if (init == null)
                {
                    Debug.LogError(Tag + " " + SpotName + "'s FSM has no Init state.");
                    return;
                }
                init.LoadActions();
                if (init.Actions.OfType<SeedsIntoSoil>().Any())
                {
                    Debug.Log(Tag + " Already done.");
                    return;
                }

                var wait = init.Actions.OfType<Wait>().FirstOrDefault();
                float waitSeconds = wait != null && wait.time != null ? wait.time.Value : 3f;
                var flight = new SeedsIntoSoil();
                flight.Reset();
                flight.gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.UseOwner };
                flight.piece = new FsmGameObject { Value = piece };
                flight.puff = new FsmGameObject { Value = puff };
                flight.seconds = Mathf.Max(0.5f, waitSeconds - 0.25f);
                init.Actions = new FsmStateAction[] { flight }.Concat(init.Actions).ToArray();
                init.SaveActions();
                EditorUtility.SetDirty(fsm);
                PrefabUtility.SaveAsPrefabAsset(root, RooftopPath);
                Debug.Log(Tag + " Done: " + SpotName + " plays the seed pieces' flight (" + flight.seconds.Value +
                          " s) in its " + waitSeconds + " s wait before the tree. No scene change.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Story/Set Up Seeds Into Soil", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
