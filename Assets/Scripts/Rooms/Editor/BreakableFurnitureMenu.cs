using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.VFX;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > 429 Game > Rooms > Set Up Breakable Furniture. The bedroom and livingroom furniture takes hits like
    /// StartGamePumpkin and behaves like the Jenga blocks:
    /// - Enemy Hit Damage (Furniture ticked): the harder the hit, the more damage (1-5, times the weapon's Damage), with
    ///   damage numbers; tumbling over or bumping into things that aren't weapons hurts only a little (Bump Share);
    /// - no jiggle: things stand on the furniture, and they'd stay still while only the furniture wobbled. An earlier
    ///   version's "Jiggle" pivot is taken out again (Remove Furniture Jiggle does only that);
    /// - the old Damage FSM stays for the smoke and the knock sound, but no longer counts hits itself (its Collision
    ///   Events and the health maths are switched off; Damage now comes from Enemy Hit Damage);
    /// - health and weight by size: the bigger the piece (its size in cubic metres, from its mesh), the heavier it is and the
    ///   more health it has (Balance below). Big furniture weighs 50 t a cubic metre, from 5 t up to 100 t (a metal Jenga
    ///   block), so a punch or a blast can still tip it, and hurts the player when it falls on them (Falling Block Damage).
    ///   Furniture the player can pick up and swing (chairs, stools, the barrel) weighs like the other weapons (200-750 kg)
    ///   and is tougher, so it lasts as a weapon; loot boxes and baskets break quickly; wall shelves keep their weight;
    /// - smoother movement when they tumble (Interpolate).
    /// The Console lists every piece's size, weight and health.
    /// Breaking, the pieces and loot they drop, the Fairy Shield clean-up and everything else in their FSMs stay as they
    /// were. Rugs, the magic tables, the radios, the spider web, pictures, candles and the small throwables (boxes, books,
    /// bottles, candy) aren't touched. Running it again sets the table's numbers again (a health changed by hand is
    /// overwritten, like Balance Damage does).
    /// </summary>
    public static class BreakableFurnitureMenu
    {
        private const string Folder = "Assets/Prefabs/BedRoom/";
        private const string HitSettingsPath = "Assets/Data/Combat/Hit Settings.asset";
        // What an earlier version added for the jiggle, taken out again.
        private const string PivotName = "Jiggle";
        private const string LookName = "Look";
        private const string ShakeState = "shake";
        // ---- Balance: health and weight from size (size = the piece's box in cubic metres, from its mesh)

        // Health = (Base + PerSize x the square root of its size) x its kind's Health Scale: a 0.1 m3 chair-sized piece
        // about 9, a 0.5 m3 table about 15, a 2 m3 wardrobe about 27.
        private const float HealthBase = 4f;
        private const float HealthPerSize = 16f;
        // Big furniture: weight from its size, between these (a metal Jenga block is 100 t).
        private const float BigMassPerSize = 50000f;
        private const float BigMassMin = 5000f;
        private const float BigMassMax = 100000f;
        // Furniture to pick up and swing: weighs like the other weapons (200-750 kg), by its size.
        private const float WeaponMassBase = 250f;
        private const float WeaponMassPerSize = 2000f;
        private const float WeaponMassMin = 200f;
        private const float WeaponMassMax = 750f;

        private enum Kind
        {
            // Stands on the floor; heavy; hurts the player when it falls on them.
            Big,
            // Can be picked up and swung or thrown: weapon weight, and tougher (it's used to hit things).
            Weapon,
            // A box or basket full of loot: weapon weight, breaks quickly.
            Loot,
            // Hangs on the wall until the walls go: its weight is left alone (Wall Hung Prop drops it).
            Wall,
        }

        private readonly struct Piece
        {
            public readonly string Name;
            public readonly Kind Kind;
            // Its health compared with others its size.
            public readonly float HealthScale;

            public Piece(string name, Kind kind, float healthScale = 1f)
            {
                Name = name;
                Kind = kind;
                HealthScale = healthScale;
            }
        }

        private static readonly Piece[] Pieces =
        {
            // Bedroom
            new("Bedroom_Bed", Kind.Big),
            new("Bedroom_Coffer", Kind.Big),
            new("Bedroom_Shelf_01", Kind.Big),
            new("MiniBedroom_Shelf_01", Kind.Big),
            new("Bedroom_Shelf_03", Kind.Big),
            new("Bedroom_Stuffs_Shelf", Kind.Big),
            new("Bedroom_Work_Table", Kind.Big),
            new("Livingroom_Table_2_small", Kind.Big),
            new("Bedroom_Bed_Stool", Kind.Big),
            new("Bedroom_Work_Table_Chair", Kind.Weapon, 1.4f),
            new("Livingroom_Chair", Kind.Weapon, 1.4f),
            new("Bedroom_Candy_Basket", Kind.Loot, 0.5f),
            // The Jenga stage's baskets hold throwing pumpkins: they open at the first good hit.
            new("Bedroom_Candy_Basket_P", Kind.Loot, 0.3f),
            new("Wood_Box", Kind.Loot, 0.5f),
            // The weapon supply stays quick to open.
            new("Wood_Box_Weapon_Main", Kind.Loot, 0.5f),
            new("Shelf_Hang_Wood", Kind.Wall),
            new("Shelf_Hang_Wood_2", Kind.Wall),
            // Sits on a wall shelf, but can be picked up and thrown: weapon weight.
            new("Bedroom_Clock", Kind.Weapon),
            // Livingroom
            new("Livingroom_Book_Shelf", Kind.Big),
            new("Livingroom_Sofa_Couple", Kind.Big),
            new("Livingroom_Sofa_Small", Kind.Big),
            new("Livingroom_Table_2", Kind.Big),
            new("Livingroom_Table_3", Kind.Big),
            new("LivingRoom_Stool", Kind.Weapon, 1.4f),
            new("Wood_Barrel", Kind.Weapon, 1.4f),
        };

        [MenuItem("Tools/429 Game/Rooms/Set Up Breakable Furniture")]
        public static void SetUp()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && Pieces.Any(p => stage.assetPath == Folder + p.Name + ".prefab"))
            {
                Debug.LogError($"[Breakable Furniture] {stage.assetPath} is open in Prefab Mode: close it first (it would " +
                               "save its old look over the change). Nothing changed.");
                return;
            }
            var settings = AssetDatabase.LoadAssetAtPath<HitSettings>(HitSettingsPath);
            if (settings == null) Debug.LogWarning("[Breakable Furniture] No " + HitSettingsPath + ": the default hit numbers are used.");

            var done = new List<string>();
            var notes = new List<string>();
            foreach (var piece in Pieces)
            {
                string path = Folder + piece.Name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    notes.Add(piece.Name + ": no prefab at " + path);
                    continue;
                }
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!SetUpPiece(root, piece, settings, notes, out string line)) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(line);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"[Breakable Furniture] {done.Count} pieces set up (size, weight, health):\n{string.Join("\n", done)}" +
                      (notes.Count > 0 ? "\nNOTES: " + string.Join("; ", notes) : ""));
        }

        [MenuItem("Tools/429 Game/Rooms/Set Up Breakable Furniture", true)]
        [MenuItem("Tools/429 Game/Rooms/Remove Furniture Jiggle", true)]
        private static bool NotPlaying() => !EditorApplication.isPlaying;

        /// <summary>
        /// Tools > 429 Game > Rooms > Remove Furniture Jiggle: the furniture stops wobbling when it's hit (things standing
        /// on it stayed still while only the furniture jiggled). Its look goes back on the piece itself and the "Jiggle"
        /// pivot and its shake go; health, weight and everything else stay as they are.
        /// </summary>
        [MenuItem("Tools/429 Game/Rooms/Remove Furniture Jiggle")]
        public static void RemoveJiggleFromAll()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && Pieces.Any(p => stage.assetPath == Folder + p.Name + ".prefab"))
            {
                Debug.LogError($"[Breakable Furniture] {stage.assetPath} is open in Prefab Mode: close it first (it would " +
                               "save its old look over the change). Nothing changed.");
                return;
            }

            var done = new List<string>();
            var notes = new List<string>();
            foreach (var piece in Pieces)
            {
                string path = Folder + piece.Name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = RemoveJiggle(root, notes);
                    var damageFsm = root.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Damage");
                    if (damageFsm != null) changed |= RemoveShake(damageFsm);
                    if (!changed) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add(piece.Name);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"[Breakable Furniture] Jiggle removed from {done.Count} pieces: {string.Join(", ", done)}" +
                      (notes.Count > 0 ? "\nNOTES: " + string.Join("; ", notes) : ""));
        }

        private static bool SetUpPiece(GameObject root, Piece piece, HitSettings settings, List<string> notes, out string line)
        {
            line = null;
            var fsms = root.GetComponents<PlayMakerFSM>();
            var healthFsm = fsms.FirstOrDefault(f => f.FsmName == "Health");
            if (!root.TryGetComponent<Rigidbody>(out var body) || healthFsm == null ||
                healthFsm.FsmVariables.FindFsmFloat("health") == null)
            {
                notes.Add(piece.Name + ": needs a Rigidbody and a Health FSM with a float 'health' on its root, skipped");
                return false;
            }
            // A Health FSM that breaks on any "Damage" event (like the radios) would break at the first hit.
            if (healthFsm.Fsm.GlobalTransitions.Any(t => t.EventName == "Damage" || t.EventName == "ShakeSmall"))
            {
                notes.Add(piece.Name + ": its Health FSM reacts to Damage itself, skipped");
                return false;
            }

            RemoveJiggle(root, notes);
            AddHitDamage(root, settings);
            // A chair or stool the player swings lasts as a weapon; a loot box or basket opens when it's thrown or smashed.
            var hits = new SerializedObject(root.GetComponent<EnemyHitDamage>());
            hits.FindProperty("wieldedShare").floatValue = piece.Kind == Kind.Loot ? 1f : 0.15f;
            hits.ApplyModifiedPropertiesWithoutUndo();
            var damageFsm = fsms.FirstOrDefault(f => f.FsmName == "Damage");
            if (damageFsm != null) QuietDamageFsm(damageFsm, piece.Name, notes);

            float size = Size(root);
            // Halved for whole-number damage (user, 2026-10-27): a punch does 1-2 instead of 1-5.
            float health = Mathf.Max(1f, Mathf.Round((HealthBase + HealthPerSize * Mathf.Sqrt(size)) * piece.HealthScale * 0.5f));
            SetHealth(healthFsm, health);
            float oldMass = body.mass;
            switch (piece.Kind)
            {
                case Kind.Big:
                    body.mass = Mathf.Round(Mathf.Clamp(size * BigMassPerSize, BigMassMin, BigMassMax) / 100f) * 100f;
                    if (!root.TryGetComponent<FallingBlockDamage>(out _)) root.AddComponent<FallingBlockDamage>();
                    break;
                case Kind.Weapon:
                case Kind.Loot:
                    body.mass = Mathf.Round(Mathf.Clamp(WeaponMassBase + size * WeaponMassPerSize, WeaponMassMin, WeaponMassMax) / 10f) * 10f;
                    break;
            }
            body.interpolation = RigidbodyInterpolation.Interpolate;
            line = $"{piece.Name} ({piece.Kind}): {size:0.00} m3, {oldMass:0} > {body.mass:0} kg, health {health:0}";
            return true;
        }

        // Its size in cubic metres: the box around its look (the mesh on its root and its plain visible parts, like a bed's
        // sheet and pillow), at the prefab's scale.
        private static float Size(GameObject root)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                bool look = filter.gameObject == root || filter.transform.parent == root.transform && IsPart(filter.transform);
                if (!look) continue;
                var m = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (any) bounds.Encapsulate(corner);
                    else bounds = new Bounds(corner, Vector3.zero);
                    any = true;
                }
            }
            if (!any) return 0.1f;
            var scale = root.transform.localScale;
            return Mathf.Abs(bounds.size.x * scale.x * bounds.size.y * scale.y * bounds.size.z * scale.z);
        }

        // A plain visible part of the piece (a bed's sheet and pillow, a basket's candy): not a spawn point, an effect, a
        // piece it breaks into, or anything with its own Rigidbody or FSM.
        private static bool IsPart(Transform child)
        {
            if (!child.gameObject.activeSelf || child.name.EndsWith("_Pos") || child.name.Contains("Drop")) return false;
            if (!child.TryGetComponent<MeshRenderer>(out _) || child.GetComponentInChildren<Rigidbody>(true) != null) return false;
            return child.GetComponentInChildren<PlayMakerFSM>(true) == null && child.GetComponentInChildren<ParticleSystem>(true) == null;
        }

        // An earlier version put its look on a "Jiggle" pivot at its foot that wobbled on hits, but things standing on the
        // furniture stayed still. The look goes back on the piece itself (where it was), its parts back under it, and the
        // pivot goes. True if there was one.
        private static bool RemoveJiggle(GameObject root, List<string> notes)
        {
            var pivot = root.transform.Find(PivotName);
            if (pivot == null || !pivot.TryGetComponent<Jiggle>(out _)) return false;

            var look = pivot.Find(LookName);
            if (look != null && look.TryGetComponent<MeshFilter>(out var lookFilter) && look.TryGetComponent<MeshRenderer>(out var lookRenderer))
            {
                // Its look sat exactly where the piece is: it goes back onto the piece's own Mesh Renderer.
                bool inPlace = (look.position - root.transform.position).sqrMagnitude < 1e-8f &&
                               Quaternion.Angle(look.rotation, root.transform.rotation) < 0.01f &&
                               (look.lossyScale - root.transform.lossyScale).sqrMagnitude < 1e-8f;
                if (inPlace && !root.TryGetComponent<MeshFilter>(out _) && !root.TryGetComponent<MeshRenderer>(out _))
                {
                    root.AddComponent<MeshFilter>().sharedMesh = lookFilter.sharedMesh;
                    EditorUtility.CopySerialized(lookRenderer, root.AddComponent<MeshRenderer>());
                    Object.DestroyImmediate(look.gameObject);
                }
                else
                {
                    notes.Add(root.name + ": its Look was moved or the piece has a mesh of its own, so the Look stays as a child");
                }
            }
            foreach (var part in pivot.Cast<Transform>().ToArray()) part.SetParent(root.transform, true);
            Object.DestroyImmediate(pivot.gameObject);
            return true;
        }

        // Takes out the "shake" state an earlier version added to the Damage FSM (a lone Jiggle Object for weak hits)
        // and the ShakeSmall transition to it: weak hits now do nothing there. True if there was one.
        private static bool RemoveShake(PlayMakerFSM component)
        {
            var fsm = component.Fsm;
            var shake = fsm.States.FirstOrDefault(s => s.Name == ShakeState);
            if (shake == null) return false;
            shake.LoadActions();
            if (shake.Actions.Length == 0 || !shake.Actions.All(a => a is JiggleObject)) return false;

            fsm.States = fsm.States.Where(s => s != shake).ToArray();
            fsm.GlobalTransitions = fsm.GlobalTransitions.Where(t => t.ToState != ShakeState).ToArray();
            foreach (var state in fsm.States)
            {
                if (state.Transitions.Any(t => t.ToState == ShakeState))
                    state.Transitions = state.Transitions.Where(t => t.ToState != ShakeState).ToArray();
            }
            EditorUtility.SetDirty(component);
            return true;
        }

        private static void AddHitDamage(GameObject root, HitSettings settings)
        {
            if (!root.TryGetComponent<EnemyHitDamage>(out var hits)) hits = root.AddComponent<EnemyHitDamage>();
            var so = new SerializedObject(hits);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("toughness").floatValue = 1f;
            so.FindProperty("hitCooldown").floatValue = 0.3f;
            so.FindProperty("knockBack").boolValue = false;
            so.FindProperty("exp").intValue = 0;
            so.FindProperty("healthFsm").stringValue = "Health";
            so.FindProperty("healthVariable").stringValue = "health";
            so.FindProperty("hitEvent").stringValue = "Damage";
            so.FindProperty("weakHitEvent").stringValue = "ShakeSmall";
            // The Damage FSM stays on for its smoke and sound (its own counting is switched off below).
            so.FindProperty("oldDamageFsm").stringValue = "";
            so.FindProperty("furniture").boolValue = true;
            so.FindProperty("bumpShare").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Through the serialized FSM: setting the variable's value in code isn't saved with the prefab.
        private static void SetHealth(PlayMakerFSM fsm, float health)
        {
            var so = new SerializedObject(fsm);
            var floats = so.FindProperty("fsm.variables.floatVariables");
            for (int i = 0; i < floats.arraySize; i++)
            {
                var variable = floats.GetArrayElementAtIndex(i);
                if (variable.FindPropertyRelative("name").stringValue != "health") continue;
                variable.FindPropertyRelative("value").floatValue = health;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // The old Damage FSM keeps its hit look and sound ("Deal 1 damage"), but Enemy Hit Damage does the counting now:
        // its Collision Events and the get / subtract / set of the health are off, and so are its punch and scale tweens
        // on the piece itself (they'd fight its physics, and shake it under the things standing on it). Damage (a hit
        // that hurts) plays "Deal 1 damage"; a hit too weak to hurt does nothing.
        private static void QuietDamageFsm(PlayMakerFSM component, string pieceName, List<string> notes)
        {
            RemoveShake(component);
            var fsm = component.Fsm;
            foreach (var state in fsm.States) state.LoadActions();

            FsmState hitState = null;
            foreach (var state in fsm.States)
            {
                bool countsHealth = state.Actions.OfType<SetFsmFloat>().Any(a => a.variableName.Value == "health");
                if (countsHealth && hitState == null) hitState = state;
                foreach (var action in state.Actions)
                {
                    switch (action)
                    {
                        case CollisionEvent:
                            action.Enabled = false;
                            break;
                        case GetFsmFloat or FloatSubtract or SetFsmFloat when countsHealth:
                            action.Enabled = false;
                            break;
                        case TweenPunch or TweenScale when OnOwner(action):
                            action.Enabled = false;
                            break;
                    }
                }
            }
            if (hitState == null)
            {
                notes.Add(pieceName + ": its Damage FSM never lowers 'health', so it has no hit state for Damage");
                hitState = fsm.States.FirstOrDefault(s => s.Name == "Deal 1 damage");
            }

            var waiting = fsm.States.FirstOrDefault(s => s.Name == "Wait for Collision") ??
                          fsm.States.FirstOrDefault(s => s.Name == fsm.StartState);
            // With its Collision Events off it would finish at once and go on to "Deal 1 damage" by its FINISHED
            // (meant for the instant enemy hits), round and round: it now just waits for Damage.
            if (waiting != null) waiting.Transitions = new FsmTransition[0];
            var damage = FsmEvent.GetFsmEvent("Damage");

            // By name: a loaded FSM's events are its own copies. Running it again adds nothing (and tidies up copies an
            // earlier version added twice).
            var events = fsm.Events.GroupBy(e => e.Name).Select(g => g.First()).ToList();
            foreach (var needed in new[] { damage, FsmEvent.Finished })
            {
                if (events.All(e => e.Name != needed.Name)) events.Add(needed);
            }
            fsm.Events = events.ToArray();

            var globals = fsm.GlobalTransitions.ToList();
            if (hitState != null && globals.All(t => t.EventName != damage.Name))
                globals.Add(new FsmTransition { FsmEvent = damage, ToState = hitState.Name });
            fsm.GlobalTransitions = globals.ToArray();

            foreach (var state in fsm.States) state.SaveActions();
            EditorUtility.SetDirty(component);
        }

        // A tween on the FSM's own object (the piece), not on one of its parts.
        private static bool OnOwner(FsmStateAction action)
        {
            var field = action.GetType().GetField("gameObject");
            return field != null && field.GetValue(action) is FsmOwnerDefault owner && owner.OwnerOption == OwnerDefaultOption.UseOwner;
        }
    }
}
