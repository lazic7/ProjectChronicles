using System;
using IsometricPathfinding.Zombies;
using UnityEngine;
using UnityEngine.UI;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class StrikePromptView : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Canvas canvas;

        [SerializeField] private Camera worldCamera;

        [SerializeField] private RectTransform promptRoot;

        [SerializeField] private Button strikeButton;

        [Header("Positioning")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.25f, 0f);

        [SerializeField] private Vector2 screenOffset;

        private ZombieAgent currentTarget;

        public event Action StrikeClicked;

        public ZombieAgent CurrentTarget => currentTarget;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            Hide();
        }

        private void OnEnable()
        {
            if (strikeButton != null)
            {
                strikeButton.onClick.AddListener(OnStrikeButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (strikeButton != null)
            {
                strikeButton.onClick.RemoveListener(OnStrikeButtonClicked);
            }
        }

        private void LateUpdate()
        {
            if (currentTarget == null)
            {
                return;
            }

            if (!promptRoot.gameObject.activeSelf)
            {
                return;
            }

            UpdatePromptPosition();
        }

        public void Show(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                Hide();
                return;
            }

            currentTarget = zombie;

            if (!promptRoot.gameObject.activeSelf)
            {
                promptRoot.gameObject.SetActive(true);
            }

            UpdatePromptPosition();
        }

        public void Hide()
        {
            currentTarget = null;

            if (promptRoot != null)
            {
                promptRoot.gameObject.SetActive(false);
            }
        }

        private void UpdatePromptPosition()
        {
            if (currentTarget == null)
            {
                Hide();
                return;
            }

            Vector3 worldPosition = currentTarget.transform.position + worldOffset;

            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                worldCamera,
                worldPosition
            );

            RectTransform canvasRectTransform = canvas.transform as RectTransform;

            if (canvasRectTransform == null)
            {
                return;
            }

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

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

            promptRoot.anchoredPosition = localPoint + screenOffset;
        }

        private void OnStrikeButtonClicked()
        {
            StrikeClicked?.Invoke();
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (canvas == null)
            {
                Debug.LogError(
                    $"{nameof(StrikePromptView)} on '{name}' is missing the Canvas reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(StrikePromptView)} on '{name}' is missing the World Camera reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (promptRoot == null)
            {
                Debug.LogError(
                    $"{nameof(StrikePromptView)} on '{name}' is missing the Prompt Root reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (strikeButton == null)
            {
                Debug.LogError(
                    $"{nameof(StrikePromptView)} on '{name}' is missing the Strike Button reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }
    }
}