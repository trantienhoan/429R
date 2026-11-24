using Game.Story;
using UnityEngine;

namespace HutongGames.PlayMaker.Actions
{
    [ActionCategory("Story")]
    [Tooltip("\"Fight\": the story's voice when the walls drop, heard in the player's head Delay seconds after the state " +
             "starts (paused time not counted). The voice is Fight Voice in Resources/Story Seed Settings; empty = " +
             "nothing. Finishes once it has played.")]
    public class StoryFightVoice : FsmStateAction
    {
        [Tooltip("Seconds after the state starts: once the walls are down (3.33 s in the rooms, 1.67 s in the Jenga room).")]
        public FsmFloat delay;

        private float waited;

        public override void Reset()
        {
            delay = 3.3f;
        }

        public override void OnEnter()
        {
            waited = 0f;
            if (StorySeedSettings.Instance.FightVoice == null || delay.Value <= 0f) Speak();
        }

        public override void OnUpdate()
        {
            waited += Time.deltaTime;
            if (waited >= delay.Value) Speak();
        }

        private void Speak()
        {
            var settings = StorySeedSettings.Instance;
            var clip = settings.FightVoice;
            if (clip != null)
            {
                var speaker = new GameObject("Story_Fight_Voice").AddComponent<AudioSource>();
                var head = Camera.main;
                if (head != null) speaker.transform.position = head.transform.position;
                speaker.playOnAwake = false;
                speaker.spatialBlend = 0f;
                speaker.PlayOneShot(clip, settings.FightVolume);
                UnityEngine.Object.Destroy(speaker.gameObject, clip.length + 0.2f);
            }
            Finish();
        }
    }
}
