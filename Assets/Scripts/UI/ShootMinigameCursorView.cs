using IsometricPathfinding.Combat;
using IsometricPathfinding.Zombies;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class ShootMinigameCursorView : MonoBehaviour
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
        [SerializeField] private Vector3 zombieHeadWorldOffset = new Vector3(0f, 0.7f, 0f);

        [SerializeField] [Min(1f)] private float horizontalTravelPixels = 100f;

        [SerializeField] [Min(1f)] private float arcDropPixels = 55f;

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

        private float currentSpeedMultiplier = 1f;

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

            SetCursorText();
            Hide();
        }

        private void OnEnable()
        {
            if (dangerTurnController != null)
            {
                dangerTurnController.ShootMinigameStarted += OnShootMinigameStarted;
                dangerTurnController.ShootMinigameEnded += OnShootMinigameEnded;
            }
        }

        private void OnDisable()
        {
            if (dangerTurnController != null)
            {
                dangerTurnController.ShootMinigameStarted -= OnShootMinigameStarted;
                dangerTurnController.ShootMinigameEnded -= OnShootMinigameEnded;
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

        private void OnShootMinigameStarted(ZombieAgent target)
        {
            if (target == null)
            {
                Hide();
                return;
            }

            currentTarget = target;
            elapsedTime = 0f;
            currentSpeedMultiplier = GetCursorSpeedMultiplier(target);
            isInsideHitWindow = false;

            SetCursorText();

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

        private void OnShootMinigameEnded()
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

            Vector2 cursorOffset = CalculateUParabolaOffset();

            cursorRoot.anchoredPosition = headLocalPosition + cursorOffset;

            isInsideHitWindow = cursorOffset.sqrMagnitude <= hitWindowPixels * hitWindowPixels;

            SetCursorColor(isInsideHitWindow ? hitColor : normalColor);
        }

        private Vector2 CalculateUParabolaOffset()
        {
            float fullTravel = horizontalTravelPixels * 2f;

            float currentCursorSpeed = cursorSpeedPixelsPerSecond * currentSpeedMultiplier;

            float horizontalOffset =
                Mathf.PingPong(elapsedTime * currentCursorSpeed, fullTravel)
                - horizontalTravelPixels;

            float normalizedHorizontalOffset = horizontalOffset / horizontalTravelPixels;

            /*
             * The zombie head is the bottom of the U-shaped parabola.
             * At x = 0, y = 0, so the O is exactly over the head and turns red.
             * At the left/right edges, y is positive, making the O travel upward
             * away from the head before returning through the hit point.
             */
            float verticalOffset =
                arcDropPixels
                * normalizedHorizontalOffset
                * normalizedHorizontalOffset;

            return new Vector2(horizontalOffset, verticalOffset);
        }

        private float GetCursorSpeedMultiplier(ZombieAgent target)
        {
            if (dangerTurnController == null)
            {
                return 1f;
            }

            return Mathf.Max(1f, dangerTurnController.GetShootCursorSpeedMultiplier(target));
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

            dangerTurnController.CompleteShootMinigame(isInsideHitWindow);
        }

        private void SetCursorText()
        {
            if (cursorText != null)
            {
                cursorText.text = "O";
            }
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
            currentSpeedMultiplier = 1f;
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
                    $"{nameof(ShootMinigameCursorView)} on '{name}' is missing the Canvas reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(ShootMinigameCursorView)} on '{name}' is missing the World Camera reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (dangerTurnController == null)
            {
                Debug.LogError(
                    $"{nameof(ShootMinigameCursorView)} on '{name}' is missing the {nameof(DangerTurnController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (cursorRoot == null)
            {
                Debug.LogError(
                    $"{nameof(ShootMinigameCursorView)} on '{name}' is missing the Cursor Root reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (cursorGraphic == null && cursorText == null)
            {
                Debug.LogError(
                    $"{nameof(ShootMinigameCursorView)} on '{name}' needs either Cursor Graphic or Cursor Text assigned.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            horizontalTravelPixels = Mathf.Max(1f, horizontalTravelPixels);
            arcDropPixels = Mathf.Max(1f, arcDropPixels);
            cursorSpeedPixelsPerSecond = Mathf.Max(1f, cursorSpeedPixelsPerSecond);
            hitWindowPixels = Mathf.Max(1f, hitWindowPixels);
            inputGraceDuration = Mathf.Max(0f, inputGraceDuration);
        }
    }
}


