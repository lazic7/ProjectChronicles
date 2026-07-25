using IsometricPathfinding.Combat;
using TMPro;
using UnityEngine;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class GameModeStatusView : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private DangerTurnController dangerTurnController;

        [SerializeField] private TextMeshProUGUI modeText;

        [Header("Display Text")]
        [SerializeField] private string dangerModeText = "DANGER!";

        [SerializeField] private string explorationModeText = "";

        private bool lastDangerModeState;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            UpdateModeText(forceUpdate: true);
        }

        private void Update()
        {
            UpdateModeText(forceUpdate: false);
        }

        private void UpdateModeText(bool forceUpdate)
        {
            bool isInDangerMode = dangerTurnController.IsInDangerMode;

            if (!forceUpdate && isInDangerMode == lastDangerModeState)
            {
                return;
            }

            lastDangerModeState = isInDangerMode;

            modeText.text = isInDangerMode
                ? dangerModeText
                : explorationModeText;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (dangerTurnController == null)
            {
                Debug.LogError(
                    $"{nameof(GameModeStatusView)} on '{name}' is missing the " +
                    $"{nameof(DangerTurnController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (modeText == null)
            {
                Debug.LogError(
                    $"{nameof(GameModeStatusView)} on '{name}' is missing the {nameof(TextMeshProUGUI)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            if (dangerModeText == null)
            {
                dangerModeText = "Danger Mode";
            }

            if (explorationModeText == null)
            {
                explorationModeText = "";
            }
        }
    }
}