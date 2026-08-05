using IsometricPathfinding.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealthBarView : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Canvas canvas;

        [SerializeField] private Camera worldCamera;

        [SerializeField] private PlayerHealthController playerHealthController;

        [SerializeField] private Transform playerTarget;

        [SerializeField] private RectTransform barRoot;

        [SerializeField] private Image fillImage;

        [Header("Positioning")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, -0.45f, 0f);

        [SerializeField] private Vector2 screenOffset;

        [Header("Animation")]
        [SerializeField] [Min(0.01f)] private float fillAnimationSpeedPerSecond = 3f;

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.25f, 1f, 0.25f, 1f);

        [SerializeField] private Color lowHealthColor = new Color(1f, 0.2f, 0.2f, 1f);

        private RectTransform canvasRectTransform;

        private Camera uiCamera;

        private GameObject barRootObject;

        private CanvasGroup selfVisibilityCanvasGroup;

        private bool usesCanvasGroupVisibility;

        private bool isBarVisible = true;

        private float displayedNormalizedHealth = 1f;

        private float targetNormalizedHealth = 1f;

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
            barRootObject = barRoot.gameObject;
            ConfigureVisibilityRoot();

            ConfigureFillImage();

            targetNormalizedHealth = playerHealthController.CurrentHealthNormalized;
            displayedNormalizedHealth = targetNormalizedHealth;

            ApplyVisualState();
            RefreshVisibility();
            UpdateBarPosition();
        }

        private void OnEnable()
        {
            if (playerHealthController != null)
            {
                playerHealthController.HealthChanged += OnHealthChanged;
            }
        }

        private void OnDisable()
        {
            if (playerHealthController != null)
            {
                playerHealthController.HealthChanged -= OnHealthChanged;
            }
        }

        private void Update()
        {
            if (barRootObject != null && isBarVisible)
            {
                UpdateBarPosition();
            }

            if (Mathf.Approximately(displayedNormalizedHealth, targetNormalizedHealth))
            {
                return;
            }

            displayedNormalizedHealth = Mathf.MoveTowards(
                displayedNormalizedHealth,
                targetNormalizedHealth,
                fillAnimationSpeedPerSecond * Time.unscaledDeltaTime
            );

            ApplyVisualState();
        }

        private void OnHealthChanged(float currentHealth, float maximumHealth)
        {
            targetNormalizedHealth = maximumHealth <= 0f
                ? 0f
                : Mathf.Clamp01(currentHealth / maximumHealth);

            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            bool shouldShow = targetNormalizedHealth < 0.9999f;
            SetBarVisible(shouldShow);

            if (shouldShow)
            {
                UpdateBarPosition();
            }
        }

        private void ConfigureVisibilityRoot()
        {
            usesCanvasGroupVisibility = barRootObject == gameObject;

            if (!usesCanvasGroupVisibility)
            {
                return;
            }

            selfVisibilityCanvasGroup = barRootObject.GetComponent<CanvasGroup>();

            if (selfVisibilityCanvasGroup == null)
            {
                selfVisibilityCanvasGroup = barRootObject.AddComponent<CanvasGroup>();
            }
        }

        private void SetBarVisible(bool shouldShow)
        {
            isBarVisible = shouldShow;

            if (barRootObject == null)
            {
                return;
            }

            if (usesCanvasGroupVisibility)
            {
                if (selfVisibilityCanvasGroup == null)
                {
                    return;
                }

                selfVisibilityCanvasGroup.alpha = shouldShow ? 1f : 0f;
                selfVisibilityCanvasGroup.interactable = shouldShow;
                selfVisibilityCanvasGroup.blocksRaycasts = shouldShow;
                return;
            }

            if (barRootObject.activeSelf != shouldShow)
            {
                barRootObject.SetActive(shouldShow);
            }
        }

        private void ApplyVisualState()
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = displayedNormalizedHealth;
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, displayedNormalizedHealth);
        }

        private void UpdateBarPosition()
        {
            if (playerTarget == null || canvasRectTransform == null)
            {
                return;
            }

            Vector3 worldPosition = playerTarget.position + worldOffset;

            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                worldCamera,
                worldPosition
            );

            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform,
                screenPosition,
                uiCamera,
                out Vector2 localPoint
            );

            if (!converted)
            {
                return;
            }

            barRoot.anchoredPosition = localPoint + screenOffset;
        }

        private void ConfigureFillImage()
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillClockwise = false;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (canvas == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHealthBarView)} on '{name}' is missing the Canvas reference.",
                    this
                );
                referencesAreValid = false;
            }

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHealthBarView)} on '{name}' is missing the World Camera reference.",
                    this
                );
                referencesAreValid = false;
            }

            if (playerHealthController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHealthBarView)} on '{name}' is missing the {nameof(PlayerHealthController)} reference.",
                    this
                );
                referencesAreValid = false;
            }

            if (playerTarget == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHealthBarView)} on '{name}' is missing the Player Target reference.",
                    this
                );
                referencesAreValid = false;
            }

            if (barRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHealthBarView)} on '{name}' is missing the Bar Root reference.",
                    this
                );
                referencesAreValid = false;
            }

            if (fillImage == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerHealthBarView)} on '{name}' is missing the Fill Image reference.",
                    this
                );
                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            fillAnimationSpeedPerSecond = Mathf.Max(0.01f, fillAnimationSpeedPerSecond);
        }
    }
}

