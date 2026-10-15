using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Menu > Set Up Start Pumpkin Tutorial. When StartGamePumpkin lands and has laughed once, the game
    /// pauses and a tutorial in three pages (the scene's Tutorial Notice, through Show Notice; once per game launch)
    /// teaches the first fight: this is your enemy, throw stuff at it, make a fist (grip + trigger) and punch it. A trigger
    /// turns the page.
    /// In pumpkin_Start's Health FSM a new state "End 1" comes between the fall (Compare Health) and the laughing
    /// (Loop Punch): it does what Punch does when it lands (removes the landing collider, bounces, looks at the player,
    /// laughs once) and then shows the tutorial; once it's closed the pumpkin laughs on as before. Running it again only
    /// updates the pages.
    /// </summary>
    public static class StartPumpkinTutorialSetup
    {
        private const string PumpkinPath = "Assets/Prefabs/Items/Breakable/StartGamePumpkin.prefab";
        private const string StateName = "End 1";
        private const string Pictures = "Assets/M_Textures/Tutorial/";

        // Title, text and picture of each page. Plain ASCII: the font atlas may not have other symbols.
        private static readonly (string title, string body, string picture)[] Pages =
        {
            // Floats above the pumpkin's head, an arrow pointing down at it.
            ("THIS IS YOUR ENEMY!", "", null),
            ("THROW STUFF AT IT!", "Throw stuff at the laughing pumpkin\nto break it!", "Tutorial_2_Throw.png"),
            ("MAKE A FIST!", "Hold GRIP + TRIGGER to make a FIST.\nUse your fist to break the laughing pumpkin!", "Tutorial_3_Fist.png"),
        };
        // The landing laugh (pumpkin_laugh_1 / 2) is 1.7 s.
        private const float AfterLaugh = 1.7f;

        [MenuItem("Tools/429 Game/Menu/Set Up Start Pumpkin Tutorial")]
        public static void SetUp()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == PumpkinPath)
            {
                Debug.LogError("[Start Pumpkin Tutorial] StartGamePumpkin is open in Prefab Mode: close it first. Nothing changed.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PumpkinPath);
            try
            {
                var pumpkin = root.transform.Find("pumpkin_Start");
                var component = pumpkin != null ? pumpkin.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Health") : null;
                if (component == null)
                {
                    Debug.LogError("[Start Pumpkin Tutorial] StartGamePumpkin has no pumpkin_Start with a Health FSM. Nothing changed.");
                    return;
                }
                var fsm = component.Fsm;
                foreach (var s in fsm.States) s.LoadActions();
                var fall = fsm.States.FirstOrDefault(s => s.Name == "Compare Health");
                var punch = fsm.States.FirstOrDefault(s => s.Name == "Punch");
                var loop = fsm.States.FirstOrDefault(s => s.Name == "Loop Punch");
                if (fall == null || punch == null || loop == null)
                {
                    Debug.LogError("[Start Pumpkin Tutorial] The Health FSM has no Compare Health, Punch or Loop Punch state. Nothing changed.");
                    return;
                }

                var end = fsm.States.FirstOrDefault(s => s.Name == StateName);
                bool made = end == null;
                if (made)
                {
                    // Punch's landing, without its crack check (Loop Punch checks right after) and without ending by
                    // itself: the notice ends it.
                    var actions = punch.Actions.Where(a => a is not FloatCompare).Select(Copy).ToList();
                    actions.Add(Notice());
                    end = new FsmState(fsm)
                    {
                        Name = StateName,
                        Position = new Rect(fall.Position.x + 200f, fall.Position.y + 60f, 100f, 32f),
                        Actions = actions.ToArray(),
                        Transitions = new[] { new FsmTransition { FsmEvent = FsmEvent.Finished, ToState = loop.Name } },
                    };
                    fsm.States = fsm.States.Append(end).ToArray();
                }
                else
                {
                    var notice = end.Actions.OfType<ShowNotice>().FirstOrDefault();
                    if (notice == null) end.Actions = end.Actions.Append(Notice()).ToArray();
                    else SetPages(notice);
                }

                // The fall now lands in End 1 (it went to Punch). By name: a loaded FSM's FINISHED is its own copy.
                foreach (var t in fall.Transitions)
                {
                    if (t.EventName == FsmEvent.Finished.Name) t.ToState = StateName;
                }

                foreach (var s in fsm.States) s.SaveActions();
                EditorUtility.SetDirty(component);
                PrefabUtility.SaveAsPrefabAsset(root, PumpkinPath);
                Debug.Log("[Start Pumpkin Tutorial] " + (made
                    ? "StartGamePumpkin: lands > End 1 (laughs once, then the tutorial notice, game paused) > Loop Punch."
                    : "The tutorial's pages are updated."));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/429 Game/Menu/Set Up Start Pumpkin Tutorial", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        private static ShowNotice Notice()
        {
            var notice = new ShowNotice();
            notice.Reset();
            SetPages(notice);
            notice.showAfter = AfterLaugh;
            notice.onceKey = "StartPumpkinTutorial";
            notice.finishEvent = FsmEvent.Finished;
            return notice;
        }

        private static void SetPages(ShowNotice notice)
        {
            notice.titles = Pages.Select(p => new FsmString { Value = p.title }).ToArray();
            notice.bodies = Pages.Select(p => new FsmString { Value = p.body }).ToArray();
            // The first page floats above the pumpkin (the FSM's owner, pumpkin_Start). Only the first time the game is
            // played on this device.
            notice.above = new FsmOwnerDefault();
            notice.pagesAbove = 1;
            notice.oncePerInstall = true;
            notice.pictures = Pages.Select(p =>
            {
                if (p.picture == null) return new FsmTexture();
                var picture = AssetDatabase.LoadAssetAtPath<Texture2D>(Pictures + p.picture);
                if (picture == null) Debug.LogWarning("[Start Pumpkin Tutorial] No picture at " + Pictures + p.picture);
                return new FsmTexture { Value = picture };
            }).ToArray();
        }

        // The same action with the same settings, but sending no events (the copied tween or sound must not end End 1).
        private static FsmStateAction Copy(FsmStateAction action)
        {
            var copy = (FsmStateAction)System.Activator.CreateInstance(action.GetType());
            foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.DeclaringType == typeof(FsmStateAction)) continue;
                field.SetValue(copy, field.FieldType == typeof(FsmEvent) ? null : field.GetValue(action));
            }
            copy.Enabled = action.Enabled;
            return copy;
        }
    }
}
