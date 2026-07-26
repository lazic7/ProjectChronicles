using IsometricPathfinding.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class SelectedZombieActionCursorLabel : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private ZombieStrikeTargetingController targetingController;

        [SerializeField] private RectTransform labelRoot;

        [SerializeField] private TextMeshProUGUI labelText;

        [Header("Positioning")]
        [SerializeField] private Vector2 screenOffset = new Vector2(18f, -18f);

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            Hide();
        }

        private void LateUpdate()
        {
            if (Mouse.current == null)
            {
                Hide();
                return;
            }

            if (targetingController == null)
            {
                Hide();
                return;
            }

            ZombieAttackOption option = targetingController.SelectedOption;

            if (option == ZombieAttackOption.None)
            {
                Hide();
                return;
            }

            Show(option);
            FollowMouse();
        }

        private void Show(ZombieAttackOption option)
        {
            if (!labelRoot.gameObject.activeSelf)
            {
                labelRoot.gameObject.SetActive(true);
            }

            labelText.text = option.ToString().ToUpperInvariant();
        }

        private void Hide()
        {
            if (labelRoot != null)
            {
                labelRoot.gameObject.SetActive(false);
            }
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