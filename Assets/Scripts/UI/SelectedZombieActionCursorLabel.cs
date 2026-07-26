using IsometricPathfinding.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class SelectedZombieActionCursorLabel : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private ZombieStrikeTargetingController targetingController;

        [SerializeField] private ZombieActionMenuView actionMenuView;

        [SerializeField] private RectTransform labelRoot;

        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Positioning")]
        [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);

        private GameObject labelRootObject;
        private ZombieAttackOption lastDisplayedOption = ZombieAttackOption.None;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            labelRootObject = labelRoot.gameObject;
            Hide();
        }

        private void LateUpdate()
        {
            if (Mouse.current == null)
            {
                Hide();
                return;
            }

            /*
             * Do not show the cursor label while the player is choosing
             * Shoot/Strike from the zombie action menu.
             */
            if (IsPointerOverUi())
            {
                Hide();
                return;
            }

            if (targetingController == null || actionMenuView == null)
            {
                Hide();
                return;
            }

            if (!targetingController.IsTargetingZombieAction)
            {
                Hide();
                return;
            }

            ZombieAttackOption option = actionMenuView.SelectedOption;

            if (option == ZombieAttackOption.None)
            {
                Hide();
                return;
            }

            Show(option);
            FollowMouse();
        }
        
        private static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private void Show(ZombieAttackOption option)
        {
            if (!labelRootObject.activeSelf)
            {
                labelRootObject.SetActive(true);
            }

            if (option != lastDisplayedOption)
            {
                lastDisplayedOption = option;
                labelText.text = option.ToString().ToUpperInvariant();
            }
        }

        private void Hide()
        {
            if (labelRootObject != null)
            {
                labelRootObject.SetActive(false);
            }

            lastDisplayedOption = ZombieAttackOption.None;
        }

        private void FollowMouse()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            labelRoot.position = mousePosition + screenOffset;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (targetingController == null)
            {
                Debug.LogError(
                    $"{nameof(SelectedZombieActionCursorLabel)} on '{name}' is missing the " +
                    $"{nameof(ZombieStrikeTargetingController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (actionMenuView == null)
            {
                Debug.LogError(
                    $"{nameof(SelectedZombieActionCursorLabel)} on '{name}' is missing the " +
                    $"{nameof(ZombieActionMenuView)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (labelRoot == null)
            {
                Debug.LogError(
                    $"{nameof(SelectedZombieActionCursorLabel)} on '{name}' is missing the Label Root reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (labelText == null)
            {
                Debug.LogError(
                    $"{nameof(SelectedZombieActionCursorLabel)} on '{name}' is missing the TextMeshProUGUI reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }
    }
}