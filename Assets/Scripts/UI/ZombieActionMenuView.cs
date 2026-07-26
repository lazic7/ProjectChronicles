using System;
using IsometricPathfinding.Combat;
using IsometricPathfinding.Zombies;
using UnityEngine;
using UnityEngine.UI;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class ZombieActionMenuView : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Canvas canvas;

        [SerializeField] private Camera worldCamera;

        [SerializeField] private RectTransform menuRoot;

        [SerializeField] private Button shootButton;

        [SerializeField] private Button strikeButton;

        [Header("Positioning")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.25f, 0f);

        [SerializeField] private Vector2 screenOffset;

        [Header("Selection Colors")]
        [SerializeField] private Color normalButtonColor = Color.white;

        [SerializeField] private Color selectedButtonColor = new Color(1f, 0.85f, 0.25f, 1f);

        private ZombieAgent currentTarget;

        private ZombieAttackOption selectedOption = ZombieAttackOption.None;

        public event Action<ZombieAttackOption> OptionSelected;

        public ZombieAgent CurrentTarget => currentTarget;

        public ZombieAttackOption SelectedOption => selectedOption;

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
            if (shootButton != null)
            {
                shootButton.onClick.AddListener(OnShootClicked);
            }

            if (strikeButton != null)
            {
                strikeButton.onClick.AddListener(OnStrikeClicked);
            }
        }

        private void OnDisable()
        {
            if (shootButton != null)
            {
                shootButton.onClick.RemoveListener(OnShootClicked);
            }

            if (strikeButton != null)
            {
                strikeButton.onClick.RemoveListener(OnStrikeClicked);
            }
        }

        private void LateUpdate()
        {
            if (currentTarget == null)
            {
                return;
            }

            if (!menuRoot.gameObject.activeSelf)
            {
                return;
            }

            UpdateMenuPosition();
        }

        public void Show(ZombieAgent zombie, ZombieAttackOption selectedAction)
        {
            if (zombie == null)
            {
                Hide();
                return;
            }

            currentTarget = zombie;
            selectedOption = selectedAction;

            if (!menuRoot.gameObject.activeSelf)
            {
                menuRoot.gameObject.SetActive(true);
            }

            RefreshButtonVisuals();
            UpdateMenuPosition();
        }

        public void Hide()
        {
            currentTarget = null;
            selectedOption = ZombieAttackOption.None;

            if (menuRoot != null)
            {
                menuRoot.gameObject.SetActive(false);
            }

            RefreshButtonVisuals();
        }

        private void OnShootClicked()
        {
            selectedOption = ZombieAttackOption.Shoot;
            RefreshButtonVisuals();
            OptionSelected?.Invoke(selectedOption);
        }

        private void OnStrikeClicked()
        {
            selectedOption = ZombieAttackOption.Strike;
            RefreshButtonVisuals();
            OptionSelected?.Invoke(selectedOption);
        }

        private void RefreshButtonVisuals()
        {
            SetButtonColor(shootButton, selectedOption == ZombieAttackOption.Shoot);
            SetButtonColor(strikeButton, selectedOption == ZombieAttackOption.Strike);
        }

        private void SetButtonColor(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Graphic targetGraphic = button.targetGraphic;

            if (targetGraphic == null)
            {
                return;
            }

            targetGraphic.color = selected
                ? selectedButtonColor
                : normalButtonColor;
        }

        private void UpdateMenuPosition()
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

            menuRoot.anchoredPosition = localPoint + screenOffset;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (canvas == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionMenuView)} on '{name}' is missing the Canvas reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionMenuView)} on '{name}' is missing the World Camera reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (menuRoot == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionMenuView)} on '{name}' is missing the Menu Root reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (shootButton == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionMenuView)} on '{name}' is missing the Shoot Button reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (strikeButton == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieActionMenuView)} on '{name}' is missing the Strike Button reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }
    }
}