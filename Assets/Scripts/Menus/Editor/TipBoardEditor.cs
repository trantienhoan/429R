using Game.Menus;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// The Tip Board's Inspector: each tip with its hint right under it. They're kept in two lists side by side (Tips
    /// and Hints, Hint 1 for Tip 1), so the buttons here add, move and remove a tip together with its hint.
    /// </summary>
    [CustomEditor(typeof(TipBoard))]
    public class TipBoardEditor : Editor
    {
        private static readonly GUIContent HintLabel = new("Hint", "Shown under the tip, smaller, once the player touches the board. Empty: none.");
        private static readonly GUIContent UpLabel = new("↑", "Move this tip (and its hint) up.");
        private static readonly GUIContent DownLabel = new("↓", "Move this tip (and its hint) down.");
        private static readonly GUIContent RemoveLabel = new("-", "Remove this tip and its hint.");

        private SerializedProperty tips;
        private SerializedProperty hints;
        private bool open = true;

        private void OnEnable()
        {
            tips = serializedObject.FindProperty("tips");
            hints = serializedObject.FindProperty("hints");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            DrawPropertiesExcluding(serializedObject, "m_Script", "tips", "hints");
            if (tips == null || hints == null)
            {
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space();
            open = EditorGUILayout.Foldout(open, $"Tips ({tips.arraySize})", true);
            if (open) DrawTips();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTips()
        {
            EditorGUILayout.HelpBox("One is picked at random every time after the First Tip. Its hint shows once the " +
                                    "player touches the board; a hard hit shows the next tip.", MessageType.None);
            int moveFrom = -1;
            int moveTo = -1;
            int remove = -1;
            int typedAt = -1;
            string typedHint = null;
            for (int i = 0; i < tips.arraySize; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Tip {i + 1}", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        using (new EditorGUI.DisabledScope(i == 0))
                        {
                            if (GUILayout.Button(UpLabel, EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
                            {
                                moveFrom = i;
                                moveTo = i - 1;
                            }
                        }
                        using (new EditorGUI.DisabledScope(i == tips.arraySize - 1))
                        {
                            if (GUILayout.Button(DownLabel, EditorStyles.miniButtonMid, GUILayout.Width(24f)))
                            {
                                moveFrom = i;
                                moveTo = i + 1;
                            }
                        }
                        if (GUILayout.Button(RemoveLabel, EditorStyles.miniButtonRight, GUILayout.Width(24f))) remove = i;
                    }
                    EditorGUILayout.PropertyField(tips.GetArrayElementAtIndex(i), GUIContent.none);
                    if (i < hints.arraySize)
                    {
                        EditorGUILayout.PropertyField(hints.GetArrayElementAtIndex(i), HintLabel);
                    }
                    else
                    {
                        // No hint yet: typing one makes room for it.
                        EditorGUI.BeginChangeCheck();
                        string typed = EditorGUILayout.TextField(HintLabel, "");
                        if (EditorGUI.EndChangeCheck())
                        {
                            typedAt = i;
                            typedHint = typed;
                        }
                    }
                }
            }
            bool add = GUILayout.Button("Add Tip");

            // Lists are changed only after everything is drawn, so this pass draws what it laid out.
            if (typedAt >= 0)
            {
                MatchHints();
                hints.GetArrayElementAtIndex(typedAt).stringValue = typedHint;
            }
            if (add)
            {
                MatchHints();
                tips.arraySize++;
                tips.GetArrayElementAtIndex(tips.arraySize - 1).stringValue = "";
                MatchHints();
            }
            else if (moveFrom >= 0)
            {
                MatchHints();
                tips.MoveArrayElement(moveFrom, moveTo);
                hints.MoveArrayElement(moveFrom, moveTo);
            }
            else if (remove >= 0)
            {
                MatchHints();
                tips.DeleteArrayElementAtIndex(remove);
                hints.DeleteArrayElementAtIndex(remove);
            }
        }

        // Gives every tip a place in Hints (empty), so the two lists line up.
        private void MatchHints()
        {
            while (hints.arraySize < tips.arraySize)
            {
                hints.arraySize++;
                // A bigger array copies its last value into the new place.
                hints.GetArrayElementAtIndex(hints.arraySize - 1).stringValue = "";
            }
        }
    }
}
