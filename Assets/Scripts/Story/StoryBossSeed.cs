using Game.Combat;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// The piece of the Green Seed a room's mini boss carries (user's story, 2026-11-25: the laughing pumpkin held a
    /// small piece of the Green Seed; its mini boss popped out of it with the piece). A green glowing seed rides on its
    /// back; when the boss dies it shrinks away (the room's chest holds the piece then). Added by Story Boss Escape to
    /// the escaping copy and to the real boss when it comes for the fight.
    /// </summary>
    public class StoryBossSeed : MonoBehaviour
    {
        private const float ShrinkSeconds = 0.4f;

        private Transform piece, anchor;
        private float above, spin, goneAt = -1f;
        private Vector3 size;
        private EnemyHitDamage hits;

        /// <summary>Puts a seed piece on this boss's back (once). Null when the settings have no piece.</summary>
        public static StoryBossSeed Give(GameObject boss)
        {
            if (boss == null || StoryBossEscapeSettings.Instance.SeedPiece == null) return null;
            if (boss.TryGetComponent(out StoryBossSeed had)) return had;
            var seed = boss.AddComponent<StoryBossSeed>();
            seed.Begin();
            return seed;
        }

        private void Begin()
        {
            var settings = StoryBossEscapeSettings.Instance;
            var seedSettings = StorySeedSettings.Instance;
            hits = GetComponent<EnemyHitDamage>();
            // It rides on its body's root bone (it moves with the body: crouches, rears up, climbs out of the ground).
            Renderer body = null;
            foreach (var look in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!look.enabled) continue;
                body = look;
                if (look.rootBone != null) anchor = look.rootBone;
                break;
            }
            if (anchor == null) anchor = transform;
            float scale = Mathf.Max(0.01f, transform.lossyScale.y);
            float top = body != null ? body.bounds.max.y : anchor.position.y;
            // Its height over the bone at size 1, so it keeps its place as the boss grows in.
            above = (Mathf.Max(0f, top - anchor.position.y) + settings.SeedAbove) / scale;

            var go = Instantiate(settings.SeedPiece, anchor.position + Vector3.up * above * scale, Quaternion.identity);
            go.name = "Story_Boss_Seed";
            StorySeedsFlight.OnlyShows(go, seedSettings.GlowParts);
            StorySeed.TintGlow(go, seedSettings.Glow, seedSettings.GlowParts);
            piece = go.transform;
            size = piece.localScale * settings.SeedSize;
            // It goes with the boss (a game over clears both).
            piece.SetParent(transform, true);
            Place();
        }

        private void LateUpdate()
        {
            if (piece == null) return;
            if (goneAt < 0f && hits != null && hits.IsDead) goneAt = Time.time;
            Place();
        }

        private void Place()
        {
            float scale = Mathf.Max(0.01f, transform.lossyScale.y);
            spin = (spin + 120f * Time.deltaTime) % 360f;
            piece.SetPositionAndRotation(anchor.position + Vector3.up * above * scale, Quaternion.Euler(0f, spin, 0f));
            float share = goneAt < 0f ? 1f : 1f - Mathf.Clamp01((Time.time - goneAt) / ShrinkSeconds);
            // A constant size in the world, whatever the boss's own scale.
            var parentScale = transform.lossyScale;
            piece.localScale = new Vector3(size.x / Mathf.Max(0.001f, parentScale.x), size.y / Mathf.Max(0.001f, parentScale.y),
                size.z / Mathf.Max(0.001f, parentScale.z)) * share;
            if (share <= 0f)
            {
                Destroy(piece.gameObject);
                piece = null;
            }
        }

        private void OnDestroy()
        {
            if (piece != null) Destroy(piece.gameObject);
        }
    }
}
