using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// What the flying slime enemies (Slime Bomber, Rainbow Slime) share: where the player's head and floor are, the
    /// player's fists, hurting the player (as a bee's bite, a share of it), and one-shot sounds.
    /// </summary>
    public static class SlimeKit
    {
        private static Game.Combat.Fist[] fists = System.Array.Empty<Game.Combat.Fist>();
        private static float fistsFoundAt = -10f;

        /// <summary>The player's head (the main camera), or null.</summary>
        public static Transform Head => Camera.main != null ? Camera.main.transform : null;

        /// <summary>The floor under the player's head (a ray down; 1.6 m below the head when it finds none).</summary>
        public static float FloorUnder(Vector3 head)
        {
            foreach (var hit in Physics.RaycastAll(head, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.7f && hit.distance > 0.4f) return hit.point.y;
            }
            return head.y - 1.6f;
        }

        /// <summary>The player's fists (looked up again now and then).</summary>
        public static Game.Combat.Fist[] Fists
        {
            get
            {
                if (Time.time - fistsFoundAt > 2f)
                {
                    fistsFoundAt = Time.time;
                    fists = Object.FindObjectsByType<Game.Combat.Fist>(FindObjectsSortMode.None);
                }
                return fists;
            }
        }

        /// <summary>A hit on the player: their Damage FSM takes it as a bee's bite, Share of it (1 = a whole bite).</summary>
        public static void HurtPlayer(float share)
        {
            var player = HutongGames.PlayMaker.FsmVariables.GlobalVariables.FindFsmGameObject("Player");
            if (player == null || player.Value == null) return;
            Game.Combat.PlayerDamageShare.Next(share);
            foreach (var fsm in player.Value.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "Damage") fsm.SendEvent("Damage");
            }
        }

        /// <summary>One of these at random, at a point (3D), a little higher or lower (Pitch, random between).</summary>
        public static void Play(AudioClip[] clips, Vector3 at, float volume, Vector2 pitch)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            var go = new GameObject("Slime Sound");
            go.transform.position = at;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(pitch.x, pitch.y);
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            source.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, source.pitch) + 0.1f);
        }

        /// <summary>
        /// One of these clips, riding along on Parent (it moves with it and goes with it), gone by itself when done; the
        /// source, so it can be stopped sooner (destroy its object). Null when there's nothing to play.
        /// </summary>
        public static AudioSource PlayOn(AudioClip[] clips, Transform parent, float volume, Vector2 pitch)
        {
            if (clips == null || clips.Length == 0 || parent == null) return null;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return null;
            var go = new GameObject("Slime Sound");
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(pitch.x, pitch.y);
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            source.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, source.pitch) + 0.1f);
            return source;
        }
    }
}
