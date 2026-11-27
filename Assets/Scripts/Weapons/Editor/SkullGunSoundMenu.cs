using UnityEditor;
using UnityEngine;

namespace Game.Weapons.EditorTools
{
    /// <summary>
    /// User 2026-11-27: "Check Skull gun, right now skull gun is using a really annoying shooting sound" and "Check Whack
    /// Pumpkin, those pumpkin in barrels, make them take less damage from gun". Re-runnable; prefabs only.
    /// - Tools > 429 Game > Weapons > Skull Gun: Softer Shots: the Skull Gun (Boong_Gun) shot was elon_boong_shoot (0.31 s,
    ///   mean -6.5 dB, peaking past full scale) on every pull; now one of Kenney's four small laser pews (0.24-0.34 s, mean
    ///   about -22 dB) at random, pitch 0.92-1.08, volume 0.7. Each skull bullet also laughs (creepy_laugh_1, 2 of 3) or
    ///   cracks 0.55-0.66 s after it's fired, and that was a 2D sound (full volume in the player's head, every bullet): now
    ///   3D (heard where the skull is, fading by 14 m) and 0.65 as loud. "Skull Gun: Old Shot Sound" puts both back.
    /// - Tools > 429 Game > Enemies > Barrel Pumpkins: Less From The Gun: the rooftop's whack pumpkins (Pumpkin_Mob_Pumpkin_6
    ///   and _7, popping out of the barrels) count a shot as a third of a whack (Fair Hits' Shot Share 0.34): three skulls
    ///   take one health; a mallet, a swing or a throw counts as before.
    /// </summary>
    internal static class SkullGunSoundMenu
    {
        private const string Gun = "Assets/Prefabs/Weapon/Boong_Gun.prefab";
        private const string Bullet = "Assets/Prefabs/Weapon/Boong_Skull_Bullet.prefab";
        private const string Pews = "Assets/Audio/Kenney Audio/kenney_sci-fi-sounds/Audio/laserSmall_00";
        private const string OldShot = "Assets/Audio/SFX/elon_boong_shoot.wav";
        private static readonly string[] BarrelPumpkins =
        {
            "Assets/Prefabs/Enemies/Pumpkin_Mob_Pumpkin_6.prefab", "Assets/Prefabs/Enemies/Pumpkin_Mob_Pumpkin_7.prefab"
        };
        private const float ShotShare = 0.34f;

        [MenuItem("Tools/429 Game/Weapons/Skull Gun: Softer Shots")]
        private static void Softer()
        {
            var pews = new AudioClip[4];
            for (int i = 0; i < pews.Length; i++) pews[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(Pews + i + ".ogg");
            if (System.Array.Exists(pews, c => c == null))
            {
                EditorUtility.DisplayDialog("Skull Gun", "Kenney's laserSmall_000-003 are missing (" + Pews + "*.ogg).", "OK");
                return;
            }
            SetGun(pews[0], new[] { pews[1], pews[2], pews[3] }, 0.7f, new Vector2(0.92f, 1.08f));
            SetBulletVoice(true, 0.65f);
            Debug.Log("[Skull Gun] Shots: laserSmall_000-003 at random, pitch 0.92-1.08, volume 0.7; the skull's laugh is 3D and quieter.");
        }

        [MenuItem("Tools/429 Game/Weapons/Skull Gun: Old Shot Sound")]
        private static void Old()
        {
            SetGun(AssetDatabase.LoadAssetAtPath<AudioClip>(OldShot), new AudioClip[0], 0.9f, Vector2.one);
            SetBulletVoice(false, 1f);
            Debug.Log("[Skull Gun] Back to elon_boong_shoot and the 2D laugh.");
        }

        // The skull lives this long (its FSM's Init: Random Wait, then Destroy Self): that sets how far it flies (it
        // leaves the gun at about 14.8 m/s). User 2026-11-27: "Reduce range of skull gun by 30%": 0.55-0.66 s -> x0.7.
        private static readonly Vector2 OldLife = new(0.55f, 0.66f);
        private const float RangeShare = 0.7f;

        [MenuItem("Tools/429 Game/Weapons/Skull Gun: Range -30%")]
        private static void ShorterRange() => SetLife(OldLife * RangeShare);

        [MenuItem("Tools/429 Game/Weapons/Skull Gun: Old Range")]
        private static void OldRange() => SetLife(OldLife);

        private static void SetLife(Vector2 seconds)
        {
            var root = PrefabUtility.LoadPrefabContents(Bullet);
            try
            {
                foreach (var fsm in root.GetComponents<PlayMakerFSM>())
                {
                    if (!fsm.Fsm.Initialized) fsm.Fsm.Init(fsm);
                    foreach (var state in fsm.FsmStates)
                    {
                        state.LoadActions();
                        bool edit = false;
                        foreach (var action in state.Actions)
                        {
                            if (action is not HutongGames.PlayMaker.Actions.RandomWait wait) continue;
                            wait.min.Value = seconds.x;
                            wait.max.Value = seconds.y;
                            edit = true;
                        }
                        if (edit) state.SaveActions();
                    }
                    EditorUtility.SetDirty(fsm);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Bullet);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[Skull Gun] The skull lives {seconds.x:0.###}-{seconds.y:0.###} s (its range).");
        }

        [MenuItem("Tools/429 Game/Enemies/Barrel Pumpkins: Less From The Gun")]
        private static void LessFromGun()
        {
            foreach (var path in BarrelPumpkins)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!root.TryGetComponent(out Game.Combat.FairHits hits))
                    {
                        Debug.LogWarning("[Barrel Pumpkins] No Fair Hits on " + path);
                        continue;
                    }
                    var so = new SerializedObject(hits);
                    so.FindProperty("shotShare").floatValue = ShotShare;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[Barrel Pumpkins] Pumpkin_Mob_Pumpkin_6 and _7: a shot counts as {ShotShare:0.##} of a whack.");
        }

        private static void SetGun(AudioClip shot, AudioClip[] more, float volume, Vector2 pitch)
        {
            var root = PrefabUtility.LoadPrefabContents(Gun);
            try
            {
                var heat = root.GetComponent<Overheat>();
                var so = new SerializedObject(heat);
                so.FindProperty("shotSound").objectReferenceValue = shot;
                var list = so.FindProperty("moreShotSounds");
                list.arraySize = more.Length;
                for (int i = 0; i < more.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = more[i];
                so.FindProperty("shotVolume").floatValue = volume;
                so.FindProperty("shotPitch").vector2Value = pitch;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Gun);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The skull's own laugh or crack as it flies: heard where it is (3D) or in the head (2D, as it was).
        private static void SetBulletVoice(bool where, float volume)
        {
            var root = PrefabUtility.LoadPrefabContents(Bullet);
            try
            {
                if (!root.TryGetComponent(out AudioSource voice)) return;
                voice.spatialBlend = where ? 1f : 0f;
                voice.volume = volume;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 1f;
                voice.maxDistance = 14f;
                voice.dopplerLevel = 0f;
                PrefabUtility.SaveAsPrefabAsset(root, Bullet);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
