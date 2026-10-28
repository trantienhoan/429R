using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Player")]
    [Tooltip("The rooftop giants' smash, once: throws the player Height metres straight up. With the Pumpkin Tree still " +
             "standing, the tree takes the whole hit (it breaks) and the player lands unhurt. With the tree gone, the " +
             "player gets Player Event when they land (Pumpkin_Boss_Damage: the Health Check, always 240).")]
    public class GiantSmash : FsmStateAction
    {
        [Tooltip("How high the player is thrown, in metres. Toughness doesn't cut it.")]
        public FsmFloat height;

        [Tooltip("The tree's tag: while one with health left stands, it takes the hit.")]
        [UIHint(UIHint.Tag)]
        public FsmString treeTag;

        [Tooltip("Sent to the player's PlayerHealth FSM when they land, if no tree took the hit.")]
        public FsmString playerEvent;

        [Tooltip("Played from the tree as it takes the hit and saves the player. Empty: none.")]
        [ObjectType(typeof(AudioClip))]
        public FsmObject treeSaveSound;

        [HasFloatSlider(0f, 1f)]
        public FsmFloat treeSaveVolume;

        [Tooltip("Played for the player (not from anywhere) as they're thrown up, tree or no tree. Empty: none.")]
        [ObjectType(typeof(AudioClip))]
        public FsmObject launchSound;

        [HasFloatSlider(0f, 1f)]
        public FsmFloat launchVolume;

        public override void Reset()
        {
            height = 5f;
            treeTag = "PumpkinTree";
            playerEvent = "Pumpkin_Boss_Damage";
            treeSaveSound = null;
            treeSaveVolume = 1f;
            launchSound = null;
            launchVolume = 1f;
        }

        public override void OnEnter()
        {
            var position = Owner != null ? Owner.transform.position : Vector3.zero;
            bool thrown = Game.Combat.PlayerLaunch.Launch(position, height.Value, 0f, true, false);
            if (thrown) Play("Launch Sound", Clip(launchSound), position, Volume(launchVolume), 0f);
            var tree = StandingTree();
            if (tree != null)
            {
                Play("Tree Save Sound", Clip(treeSaveSound), tree.transform.position, Volume(treeSaveVolume), 0.5f);
                Break(tree);
            }
            else if (thrown) Game.Combat.PlayerLaunch.HurtOnLanding(playerEvent.Value);
            Finish();
        }

        private static AudioClip Clip(FsmObject sound) => sound != null ? sound.Value as AudioClip : null;

        private static float Volume(FsmFloat volume) => volume != null && !volume.IsNone ? volume.Value : 1f;

        // A one-shot on its own object (the roof and the tree are gone a moment later). Blend 0: for the player, not
        // from anywhere; 0.5: from At but heard all over the roof (full loudness for 8 m).
        private static void Play(string name, AudioClip clip, Vector3 at, float volume, float blend)
        {
            if (clip == null) return;
            var speaker = new GameObject(name);
            speaker.transform.position = at;
            var source = speaker.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = blend;
            source.minDistance = 8f;
            source.maxDistance = 60f;
            source.dopplerLevel = 0f;
            source.Play();
            UnityEngine.Object.Destroy(speaker, clip.length + 0.1f);
        }

        // The tree with health left, if one stands.
        private PlayMakerFSM StandingTree()
        {
            if (string.IsNullOrEmpty(treeTag.Value)) return null;
            foreach (var thing in GameObject.FindGameObjectsWithTag(treeTag.Value))
            {
                foreach (var fsm in thing.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName != "Health") continue;
                    var health = fsm.FsmVariables.FindFsmFloat("health");
                    if (health != null && health.Value > 0f) return fsm;
                }
            }
            return null;
        }

        // Its Health FSM breaks it the usual way (Dead). No EXP: the giants broke it, not the player.
        private static void Break(PlayMakerFSM health)
        {
            Game.Leveling.PlayerLevel.NoRewardFor(health.gameObject);
            health.SendEvent("Dead");
            health.FsmVariables.FindFsmFloat("health").Value = 0f;
        }
    }
}
