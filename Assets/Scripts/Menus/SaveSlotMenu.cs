using System;
using System.Collections;
using System.Globalization;
using Game.Locomotion;
using Game.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Menus
{
    /// <summary>
    /// The start menu: when the game starts (after the health warning) the player picks one of three save slots in front
    /// of them. Each slot shows "Empty", or the level and room of its last checkpoint and when it was saved. Clicking a
    /// slot selects it; Start Game starts the selected slot (a new game if it's empty, else from its last checkpoint).
    /// Holding the trigger on a slot fills it red and starts it after 2 s (1 s with both triggers held). Delete Save
    /// (smaller) empties the selected slot after asking. Starting fades the view to black and shows the loading screen
    /// (Save Notice: the game saves by itself at every checkpoint) for a few seconds while the game, held meanwhile, puts
    /// the player at the slot's checkpoint (GAMESTAGES' Go To Checkpoint), then fades back in. Once a slot is picked it's
    /// remembered until the game closes, so Re-Dream skips this menu. It glides back in front of the player if they look
    /// away, and hides while the pause menu is open. Tools > 429 Game > Menu > Set Up Save Slot Menu builds it.
    /// </summary>
    public class SaveSlotMenu : MonoBehaviour
    {
        /// <summary>One save slot's button.</summary>
        [Serializable]
        public class Slot
        {
            public Button button;
            [Tooltip("Coloured to show the selected slot.")]
            public Image background;
            [Tooltip("Shown on the selected slot (an outline around it).")]
            public Behaviour selectedMark;
            [Tooltip("\"Slot 1\".")]
            public TMP_Text title;
            [Tooltip("\"Empty\", or e.g. \"Lv 5 - Living Room\".")]
            public TMP_Text info;
            [Tooltip("When it was saved.")]
            public TMP_Text date;
            [Tooltip("Holding the slot starts it.")]
            public PressAndHold hold;
        }

        /// <summary>A short room name for a checkpoint, e.g. "CP LivingRoom" = "Living Room".</summary>
        [Serializable]
        public struct RoomName
        {
            public string checkpoint;
            public string name;

            public RoomName(string checkpoint, string name)
            {
                this.checkpoint = checkpoint;
                this.name = name;
            }
        }

        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Slot[] slots = new Slot[SaveManager.SlotCount];
        [SerializeField] private Button startButton;
        [SerializeField] private Button deleteButton;

        [Header("Delete box")]
        [Tooltip("The box over the menu that asks before deleting; hidden until Delete Save is pressed.")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmDeleteButton;
        [SerializeField] private Button confirmCancelButton;

        [Header("Words")]
        [SerializeField] private string slotTitle = "Slot {0}";
        [SerializeField] private string emptyLine = "Empty";
        [Tooltip("A slot whose save can't be read (picking it starts a new game; a copy of the file is kept).")]
        [SerializeField] private string damagedLine = "Damaged";
        [Tooltip("{0} = the level, {1} = the room.")]
        [SerializeField] private string savedLine = "Lv {0} - {1}";
        [SerializeField] private string confirmLine = "Delete Slot {0}?";
        [Tooltip("The room shown for a save with no checkpoint yet.")]
        [SerializeField] private string startRoom = "Start";
        [Tooltip("Short room names for the checkpoints (GAMESTAGES' Set Checkpoint names). Others show without \"CP \".")]
        [SerializeField] private RoomName[] roomNames =
        {
            new("CP Bedroom", "Bedroom"),
            new("CP 2.5 - BR", "Bedroom"),
            new("CP LivingRoom", "Living Room"),
            new("CP Scale LR", "Living Room"),
            new("CP Rooftop", "Rooftop"),
            new("CP Boss", "Boss"),
        };

        [Header("Look")]
        [SerializeField] private Color slotColor = new(0.2f, 0.17f, 0.32f, 1f);
        [SerializeField] private Color selectedColor = new(0.36f, 0.28f, 0.6f, 1f);
        [SerializeField] private Color infoColor = Color.white;
        [SerializeField] private Color emptyColor = new(1f, 1f, 1f, 0.5f);

        [Header("Starting")]
        [Tooltip("Seconds the view takes to fade to black.")]
        [Min(0f)]
        [SerializeField] private float fadeOutSeconds = 0.6f;
        [Tooltip("Seconds the loading screen shows at least, so it can be read.")]
        [Min(0f)]
        [SerializeField] private float loadingSeconds = 3f;
        [Tooltip("Seconds the view takes to fade back in.")]
        [Min(0f)]
        [SerializeField] private float fadeInSeconds = 0.8f;
        [Tooltip("Longest wait in the black for GAMESTAGES to put the player at the checkpoint (Go To Checkpoint).")]
        [Min(0f)]
        [SerializeField] private float longestWait = 10f;

        [Header("Placement")]
        [Tooltip("The headset camera. Left empty, Camera.main is used.")]
        [SerializeField] private Transform head;
        [Tooltip("Where the menu sits, in metres from your head (x right, y up, z forward).")]
        [SerializeField] private Vector3 panelOffset = new(0f, -0.05f, 1.3f);
        [Tooltip("Looking further away than this (degrees), the menu glides back in front of you.")]
        [SerializeField] private float followAngle = 35f;
        [Tooltip("Seconds to fade in and out.")]
        [Min(0f)]
        [SerializeField] private float fadeTime = 0.3f;
        [Tooltip("Lets the mouse click the menu in the Editor and without a headset; switched off when a headset is used in a build.")]
        [SerializeField] private GraphicRaycaster mouseRaycaster;

        [Header("Sounds (heard while the game's sound is paused)")]
        [SerializeField] private AudioClip clickSound;
        [SerializeField] private AudioClip startSound;
        [SerializeField] private AudioClip deleteSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.6f;

        [Header("Testing")]
        [Tooltip("In the Editor only: start this save slot at once, without the menu or the loading screen (0: show the menu).")]
        [Range(0, SaveManager.SlotCount)]
        [SerializeField] private int editorStartSlot;

        private enum Step { Waiting, Open, Starting, Done }

        private Step step = Step.Waiting;
        private int selected = -1;
        private SaveManager.SlotInfo[] infos = new SaveManager.SlotInfo[SaveManager.SlotCount];
        private bool gliding;
        private bool holding;
        private AudioSource sound;

        /// <summary>Whether the menu is up (or a slot is starting: the loading screen).</summary>
        public bool IsBusy => step == Step.Open || step == Step.Starting;

        private void Awake()
        {
            if (panel != null)
            {
                panel.alpha = 0f;
                panel.gameObject.SetActive(false);
            }
            if (confirmPanel != null) confirmPanel.SetActive(false);

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                int index = i;
                if (slot.button != null) slot.button.onClick.AddListener(() => Select(index, true));
                if (slot.hold != null) slot.hold.Held += () => StartSlot(index);
            }
            if (startButton != null) startButton.onClick.AddListener(() => StartSlot(selected));
            if (deleteButton != null) deleteButton.onClick.AddListener(AskDelete);
            if (confirmDeleteButton != null) confirmDeleteButton.onClick.AddListener(DeleteSelected);
            if (confirmCancelButton != null) confirmCancelButton.onClick.AddListener(HideConfirm);

            // A headset's rays use the tracked-device raycaster; the mouse needs this one (Editor tests, no headset).
            if (mouseRaycaster != null)
                mouseRaycaster.enabled = Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive;

            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0f;
            sound.ignoreListenerPause = true;
        }

        private void OnDisable()
        {
            // Never leave the game held or the pause menu blocked (e.g. the scene closing while loading).
            if (holding)
            {
                holding = false;
                PauseMenu.Release(this);
            }
            PauseMenu.AllowOpening(this);
            if (step == Step.Starting)
            {
                var notice = SaveNotice.Find();
                if (notice != null) notice.ShowLoading(false);
                var fade = HeadsetFade.Find();
                if (fade != null) fade.FadeIn(0f);
                step = Step.Done;
            }
        }

        private void Update()
        {
            switch (step)
            {
                case Step.Waiting:
                    WaitToOpen();
                    break;
                case Step.Open:
                    UpdateOpen();
                    break;
            }
        }

        // Opens once nothing else is in front (the health warning), unless a slot is already being played.
        private void WaitToOpen()
        {
            // The scene reloaded (Re-Dream): the Save Manager goes on in the same slot by itself.
            if (SaveManager.CurrentSlot > 0)
            {
                Finish();
                return;
            }
#if UNITY_EDITOR
            if (editorStartSlot > 0)
            {
                SaveManager.ChooseSlot(editorStartSlot);
                Finish();
                return;
            }
#endif
            if (HealthWarning.IsShowing) return;
            Open();
        }

        private void Open()
        {
            if (panel == null)
            {
                Debug.LogWarning("[Save Slot Menu] No panel: save slot 1 is used. Run Tools > 429 Game > Menu > Set Up Save Slot Menu.", this);
                SaveManager.ChooseSlot(1);
                Finish();
                return;
            }

            step = Step.Open;
            Refresh();
            // The last slot played is ready to go: Start Game starts it.
            Select(MostRecentSlot(), false);
            HideConfirm();
            panel.alpha = 0f;
            panel.gameObject.SetActive(true);
            gliding = false;
            if (TryGetPlacement(out var position, out var rotation)) panel.transform.SetPositionAndRotation(position, rotation);
        }

        private void UpdateOpen()
        {
            // The pause menu sits in front: this one hides (and can't be pressed) until it's closed.
            bool show = !PauseMenu.IsPaused;
            float rate = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            panel.alpha = Mathf.MoveTowards(panel.alpha, show ? 1f : 0f, rate);
            panel.interactable = show;
            panel.blocksRaycasts = show;
            if (!show) CancelHolds();
            FollowGaze();
        }

        /// <summary>Re-reads what each save slot holds.</summary>
        public void Refresh()
        {
            for (int i = 0; i < slots.Length && i < infos.Length; i++)
            {
                var info = SaveManager.SlotSummary(i + 1);
                infos[i] = info;
                var slot = slots[i];
                if (slot == null) continue;

                if (slot.title != null) slot.title.text = string.Format(slotTitle, i + 1);
                if (slot.info != null)
                {
                    slot.info.text = info.HasSave ? string.Format(savedLine, info.Level, RoomOf(info.Checkpoint))
                        : info.Damaged ? damagedLine : emptyLine;
                    slot.info.color = info.HasSave ? infoColor : emptyColor;
                }
                if (slot.date != null) slot.date.text = info.HasSave ? DateLine(info.SavedAt) : "";
            }
            ShowSelection();
        }

        /// <summary>The short room name for a checkpoint, e.g. "CP LivingRoom" = "Living Room" ("" = the start).</summary>
        public string RoomOf(string checkpoint)
        {
            if (string.IsNullOrEmpty(checkpoint)) return startRoom;
            if (roomNames != null)
            {
                foreach (var room in roomNames)
                {
                    if (string.Equals(room.checkpoint, checkpoint, StringComparison.OrdinalIgnoreCase)) return room.name;
                }
            }
            return checkpoint.StartsWith("CP ", StringComparison.OrdinalIgnoreCase) ? checkpoint.Substring(3).Trim() : checkpoint;
        }

        // "Today 14:05", "Yesterday 09:30", or "19 Oct 14:05".
        private static string DateLine(DateTime at)
        {
            if (at == DateTime.MinValue) return "";
            var today = DateTime.Now.Date;
            string time = at.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (at.Date == today) return "Today " + time;
            if (at.Date == today.AddDays(-1)) return "Yesterday " + time;
            return at.ToString(at.Year == today.Year ? "d MMM HH:mm" : "d MMM yyyy", CultureInfo.InvariantCulture);
        }

        // The slot saved last, or -1 with no saves.
        private int MostRecentSlot()
        {
            int best = -1;
            for (int i = 0; i < infos.Length; i++)
            {
                if (!infos[i].HasSave) continue;
                if (best < 0 || infos[i].SavedAt > infos[best].SavedAt) best = i;
            }
            return best;
        }

        private void Select(int index, bool click)
        {
            if (step != Step.Open && click) return;
            // The delete box names the selected slot: a click behind it (e.g. a second ray in the same frame it opened)
            // mustn't change which slot Delete empties.
            if (click && confirmPanel != null && confirmPanel.activeSelf) return;
            selected = index >= 0 && index < slots.Length ? index : -1;
            if (click) Play(clickSound);
            ShowSelection();
        }

        private void ShowSelection()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                bool on = i == selected;
                if (slot.background != null) slot.background.color = on ? selectedColor : slotColor;
                if (slot.selectedMark != null) slot.selectedMark.enabled = on;
            }
            bool any = selected >= 0;
            if (startButton != null) startButton.interactable = any;
            if (deleteButton != null) deleteButton.interactable = any && selected < infos.Length && (infos[selected].HasSave || infos[selected].Damaged);
        }

        private void AskDelete()
        {
            if (step != Step.Open || selected < 0 || confirmPanel == null) return;
            CancelHolds();
            if (confirmText != null) confirmText.text = string.Format(confirmLine, selected + 1);
            confirmPanel.SetActive(true);
            Play(clickSound);
        }

        private void HideConfirm()
        {
            if (confirmPanel != null && confirmPanel.activeSelf)
            {
                confirmPanel.SetActive(false);
                if (step == Step.Open) Play(clickSound);
            }
        }

        private void DeleteSelected()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (step != Step.Open || selected < 0) return;
            if (SaveManager.DeleteSlot(selected + 1)) Play(deleteSound);
            Refresh();
        }

        private void CancelHolds()
        {
            foreach (var slot in slots)
            {
                if (slot != null && slot.hold != null && (slot.hold.IsPressed || slot.hold.Progress > 0f)) slot.hold.Cancel();
            }
        }

        /// <summary>Starts a save slot (0-based index): a new game if it's empty, else from its last checkpoint.</summary>
        public void StartSlot(int index)
        {
            if (step != Step.Open || index < 0 || index >= slots.Length) return;
            if (confirmPanel != null && confirmPanel.activeSelf) return;
            step = Step.Starting;
            selected = index;
            ShowSelection();
            StartCoroutine(Starting(index + 1));
        }

        private IEnumerator Starting(int slot)
        {
            Play(startSound);
            // The pause menu would open hidden behind the black.
            PauseMenu.BlockOpening(this);
            panel.interactable = false;
            panel.blocksRaycasts = false;
            CancelHolds();

            // The menu and the view fade out together.
            var fade = HeadsetFade.Find();
            if (fade != null) fade.FadeOut(fadeOutSeconds);
            float startAlpha = panel.alpha;
            for (float t = 0f; t < fadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                panel.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeOutSeconds);
                yield return null;
            }
            while (fade != null && fade.IsFading) yield return null;
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);

            var notice = SaveNotice.Find();
            if (notice != null) notice.ShowLoading(true);
            float shownAt = Time.unscaledTime;
            SaveManager.ChooseSlot(slot);

            // GAMESTAGES' Go To Checkpoint puts the player at the slot's checkpoint (or the start) during the black.
            while (!Checkpoints.HasResumed && Time.unscaledTime - shownAt < longestWait) yield return null;
            // Then nothing runs until the view is back.
            holding = true;
            PauseMenu.Hold(this);
            while (Time.unscaledTime - shownAt < loadingSeconds) yield return null;

            if (notice != null) notice.ShowLoading(false);
            if (fade != null) fade.FadeIn(fadeInSeconds);
            holding = false;
            PauseMenu.Release(this);
            PauseMenu.AllowOpening(this);
            Finish();
        }

        private void Finish()
        {
            step = Step.Done;
            if (panel != null) panel.gameObject.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            enabled = false;
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && sound != null) sound.PlayOneShot(clip, volume);
        }

        // Stays put while you read, and glides back (no snapping, which is uncomfortable in VR) once you look away.
        private void FollowGaze()
        {
            if (!TryGetPlacement(out var position, out var rotation)) return;

            var toPanel = panel.transform.position - head.position;
            var wanted = position - head.position;
            if (!gliding && (Vector3.Angle(toPanel, wanted) > followAngle || Mathf.Abs(toPanel.magnitude - wanted.magnitude) > 0.5f))
                gliding = true;
            if (!gliding) return;

            float blend = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            panel.transform.SetPositionAndRotation(
                Vector3.Lerp(panel.transform.position, position, blend),
                Quaternion.Slerp(panel.transform.rotation, rotation, blend));
            if (Vector3.Distance(panel.transform.position, position) < 0.02f) gliding = false;
        }

        private bool TryGetPlacement(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null) return false;

            // Face the way the head looks, ignoring up/down tilt (looking down, the head's up points forward).
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            var yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            position = head.position + yaw * panelOffset;
            // A world-space canvas is readable when its forward points away from the viewer.
            rotation = Quaternion.LookRotation(position - head.position, Vector3.up);
            return true;
        }
    }
}
