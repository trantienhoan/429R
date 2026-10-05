using UnityEngine;

namespace Game.PlayGround
{
    /// <summary>
    /// Marks something the cauldron cooks with, e.g. a lollipop or a Pumpkin_On_Tree. Dropped or thrown into the
    /// cauldron, it's taken in and counted towards the recipe. Its picture shows on the magic hat's recipe card.
    /// </summary>
    [DisallowMultipleComponent]
    public class CookingIngredient : MonoBehaviour
    {
        [Tooltip("What it counts as in a recipe, e.g. Lolipop_1. Left empty, its prefab name is used.")]
        [SerializeField] private string ingredientId;
        [Tooltip("Its picture on the recipe card.")]
        [SerializeField] private Sprite icon;

        public string Id => string.IsNullOrEmpty(ingredientId) ? BaseName(name) : ingredientId;
        public Sprite Icon => icon;

        // "Lolipop_1(Clone)" and "Lolipop_1 (2)" are both Lolipop_1.
        public static string BaseName(string objectName)
        {
            int bracket = objectName.IndexOf('(');
            return (bracket >= 0 ? objectName.Substring(0, bracket) : objectName).Trim();
        }
    }
}
