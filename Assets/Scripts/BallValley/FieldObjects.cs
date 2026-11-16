using UnityEngine;
using ValleyGame = Balls.BallValleyStage;   // 429R: the gate stage took BALLS' ValleyGame's place

namespace Balls
{
    /// <summary>Rocks, trees, rim walls: hitting one hard costs the main ball baby balls. Rocks and trees (prop set) can be broken.</summary>
    public class Hazard : MonoBehaviour
    {
        public float damageScale = 1f;
        public bool wood;   // trees: wooden crash sound instead of rock
        /// <summary>The tree or boulder this collider belongs to (null for the rim walls, which never break).</summary>
        public FieldProp prop;
    }

    /// <summary>Breakable field stuff (pumpkins, giant mushrooms). Smash it and it bursts into food.</summary>
    public class Destructible : MonoBehaviour
    {
        public string kind;   // 429R: no netId
        public float hp, maxHp;
        public int foodDrop;
        public Color foodColor, chipColor;
        public Transform vis;

        Vector3 baseScale;
        float wobble;
        float appear;
        bool dead;

        void Start()
        {
            if (vis) baseScale = vis.localScale;
        }

        void Update()
        {
            if (!vis) return;
            appear = Mathf.Min(1f, appear + Time.deltaTime * 3f);
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 3f);
            float w = Mathf.Sin(Time.time * 38f) * 0.14f * wobble;
            float hpLook = Mathf.Lerp(0.8f, 1f, hp / Mathf.Max(1f, maxHp));
            vis.localScale = new Vector3(baseScale.x * (1f + w), baseScale.y * (1f - w), baseScale.z * (1f + w)) * (appear * hpLook);
        }

        // 429R: no network copies (BALLS' ProxyHp).

        public void Hit(float dmg, Vector3 point, BallPlayer by)
        {
            if (dead || dmg <= 0f) return;
            hp -= dmg;
            wobble = 1f;
            Gfx.Sparks(point, chipColor, Mathf.Clamp(Mathf.RoundToInt(dmg), 3, 14), 6f);
            if (hp <= 0f) Break();
        }

        void Break()
        {
            dead = true;
            Vector3 c = transform.position;
            for (int i = 0; i < foodDrop; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(3f, 9f) + Vector3.up * Random.Range(6f, 11f);
                ValleyGame.I.SpawnPellet(new Vector3(c.x, 1.2f, c.z), v, foodColor);
            }
            Gfx.Puff(c + Vector3.up, chipColor, 14, 2.5f, 4f);
            ValleyGame.I.destructibles.Remove(this);
            Destroy(gameObject);
        }
    }
}
