using IsometricPathfinding.Combat;
using IsometricPathfinding.Zombies;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class StrikeMinigameCursorView : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Canvas canvas;

        [SerializeField] private Camera worldCamera;

        [SerializeField] private DangerTurnController dangerTurnController;

        [SerializeField] private ZombieStrikeTargetingController targetingController;

        [SerializeField] private RectTransform cursorRoot;

        [SerializeField] private TextMeshProUGUI cursorText;

        [SerializeField] private Graphic cursorGraphic;

        [Header("Cursor Position")]
        [SerializeField] private Vector3 zombieHeadWorldOffset = new Vector3(0f, 1.05f, 0f);

        [SerializeField] [Min(1f)] private float verticalTravelPixels = 90f;

        [SerializeField] [Min(1f)] private float cursorSpeedPixelsPerSecond = 260f;

        [SerializeField] [Min(1f)] private float hitWindowPixels = 18f;

        [Header("Input")]
        [SerializeField] [Min(0f)] private float inputGraceDuration = 0.15f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;

        [SerializeField] private Color hitColor = Color.red;

        private ZombieAgent currentTarget;

        private RectTransform canvasRectTransform;

        private Camera uiCamera;

        private GameObject cursorRootObject;

        private float elapsedTime;

        private bool isInsideHitWindow;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            canvasRectTransform = canvas.transform as RectTransform;
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            cursorRootObject = cursorRoot.gameObject;

            if (cursorText != null)
            {
                cursorText.text = "X";
            }

            Hide();
        }

        private void OnEnable()
        {
            if (dangerTurnController != null)
            {
                dangerTurnController.StrikeMinigameStarted += OnStrikeMinigameStarted;
                dangerTurnController.StrikeMinigameEnded += OnStrikeMinigameEnded;
            }
        }

        private void OnDisable()
        {
            if (dangerTurnController != null)
            {
                dangerTurnController.StrikeMinigameStarted -= OnStrikeMinigameStarted;
                dangerTurnController.StrikeMinigameEnded -= OnStrikeMinigameEnded;
            }

            Hide();
        }

        private void Update()
        {
            if (currentTarget == null)
            {
                return;
            }

            if (currentTarget.State == ZombieState.Dead)
            {
                Hide();
                return;
            }

            elapsedTime += Time.unscaledDeltaTime;

            UpdateCursorPositionAndColor();
            HandleClick();
        }

        private void OnStrikeMinigameStarted(ZombieAgent target)
        {
            if (target == null)
            {
                Hide();
                return;
            }

            currentTarget = target;
            elapsedTime = 0f;
            isInsideHitWindow = false;

            if (targetingController != null)
            {
                targetingController.ForceClearTargeting();
            }

            if (!cursorRootObject.activeSelf)
            {
                cursorRootObject.SetActive(true);
            }

            UpdateCursorPositionAndColor();
        }

        private void OnStrikeMinigameEnded()
        {
            Hide();
        }

        private void UpdateCursorPositionAndColor()
        {
            if (currentTarget == null)
            {
                Hide();
                return;
            }

            Vector3 headWorldPosition = currentTarget.transform.position + zombieHeadWorldOffset;

            Vector2 headScreenPosition = RectTransformUtility.WorldToScreenPoint(
                worldCamera,
                headWorldPosition
            );

            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform,
                headScreenPosition,
                uiCamera,
                out Vector2 headLocalPosition
            );

            if (!converted)
            {
                return;
            }

            float fullTravel = verticalTravelPixels * 2f;

            float verticalOffset =
                Mathf.PingPong(elapsedTime * cursorSpeedPixelsPerSecond, fullTravel)
                - verticalTravelPixels;

            cursorRoot.anchoredPosition = headLocalPosition + new Vector2(0f, verticalOffset);

            isInsideHitWindow = Mathf.Abs(verticalOffset) <= hitWindowPixels;

            SetCursorColor(isInsideHitWindow ? hitColor : normalColor);
        }

        private void HandleClick()
        {
            if (Mouse.current == null)
            {
                return;
            }

            /*
             * Prevent the click that started the minigame from instantly
             * resolving the minigame on the same frame.
             */
            if (elapsedTime < inputGraceDuration)
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (dangerTurnController == null)
            {
                return;
            }

            dangerTurnController.CompleteStrikeMinigame(isInsideHitWindow);
        }

        private void SetCursorColor(Color color)
        {
            if (cursorGraphic != null)
            {
                cursorGraphic.color = color;
            }

            if (cursorText != null)
            {
                cursorText.color = color;
            }
        }

        private void Hide()
        {
            currentTarget = null;
            elapsedTime = 0f;
            isInsideHitWindow = false;

            SetCursorColor(normalColor);

            if (cursorRootObject != null)
            {
                cursorRootObject.SetActive(false);
            }
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (canvas == null)
            {
                Debug.LogError(
                    $"{nameof(StrikeMinigameCursorView)} on '{name}' is missing the Canvas reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(StrikeMinigameCursorView)} on '{name}' is missing the World Camera reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (dangerTurnController == null)
            {
                Debug.LogError(
                    $"{nameof(StrikeMinigameCursorView)} on '{name}' is missing the {nameof(DangerTurnController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (cursorRoot == null)
            {
                Debug.LogError(
                    $"{nameof(StrikeMinigameCursorView)} on '{name}' is missing the Cursor Root reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (cursorGraphic == null && cursorText == null)
            {
                Debug.LogError(
                    $"{nameof(StrikeMinigameCursorView)} on '{name}' needs either Cursor Graphic or Cursor Text assigned.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            verticalTravelPixels = Mathf.Max(1f, verticalTravelPixels);
            cursorSpeedPixelsPerSecond = Mathf.Max(1f, cursorSpeedPixelsPerSecond);
            hitWindowPixels = Mathf.Max(1f, hitWindowPixels);
            inputGraceDuration = Mathf.Max(0f, inputGraceDuration);
        }
    }
}