using IsometricPathfinding.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerDefeatModalView : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlayerHealthController playerHealthController;

        [SerializeField] private GameObject modalRoot;

        [SerializeField] private Button goBackToCampButton;

        [SerializeField] private Button restoreAndKeepPlayingButton;

        [Header("Optional Text References")]
        [SerializeField] private TextMeshProUGUI titleText;

        [SerializeField] private TextMeshProUGUI messageText;

        [SerializeField] private TextMeshProUGUI restoreButtonText;

        [Header("Display Text")]
        [SerializeField] private string defeatedTitle = "YOU ARE DEAD";

        [SerializeField] private string defeatedMessage =
            "Restore your character to keep playing or go back to camp.";

        [SerializeField] private string restoreButtonLabel = "RESTORE & KEEP PLAYING";

        [SerializeField] [Min(0)] private int restoreCost = 5;

        [Header("Scene Navigation")]
        [SerializeField] private string goBackToCampSceneName = "";

        [Header("Behavior")]
        [SerializeField] private bool pauseGameTimeWhileModalIsOpen = true;

        [Header("Optional Events")]
        [SerializeField] private UnityEvent restoredAndKeptPlaying;

        [SerializeField] private UnityEvent goBackToCampRequested;

        private float timeScaleBeforeModal = 1f;

        private bool hasPausedTimeForModal;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            ApplyDisplayText();
            HideModal();

            if (playerHealthController.IsDefeated)
            {
                ShowModal();
            }
        }

        private void OnEnable()
        {
            if (playerHealthController != null)
            {
                playerHealthController.Defeated += OnPlayerDefeated;
            }

            if (goBackToCampButton != null)
            {
                goBackToCampButton.onClick.AddListener(OnGoBackToCampClicked);
            }

            if (restoreAndKeepPlayingButton != null)
            {
                restoreAndKeepPlayingButton.onClick.AddListener(OnRestoreAndKeepPlayingClicked);
            }
        }

        private void OnDisable()
        {
            if (playerHealthController != null)
            {
                playerHealthController.Defeated -= OnPlayerDefeated;
            }

            if (goBackToCampButton != null)
            {
                goBackToCampButton.onClick.RemoveListener(OnGoBackToCampClicked);
            }

            if (restoreAndKeepPlayingButton != null)
            {
                restoreAndKeepPlayingButton.onClick.RemoveListener(OnRestoreAndKeepPlayingClicked);
            }

            ResumeTimeIfPausedByModal();
        }

        private void OnPlayerDefeated()
        {
            ShowModal();
        }

        private void ShowModal()
        {
            ApplyDisplayText();

            if (modalRoot != null && !modalRoot.activeSelf)
            {
                modalRoot.SetActive(true);
            }

            PauseTimeIfConfigured();
        }

        private void HideModal()
        {
            if (modalRoot != null && modalRoot.activeSelf)
            {
                modalRoot.SetActive(false);
            }

            ResumeTimeIfPausedByModal();
        }

        private void OnRestoreAndKeepPlayingClicked()
        {
            if (playerHealthController != null)
            {
                playerHealthController.RestoreFullHealth();
            }

            HideModal();
            restoredAndKeptPlaying?.Invoke();
        }

        private void OnGoBackToCampClicked()
        {
            goBackToCampRequested?.Invoke();
            ResumeTimeIfPausedByModal();

            if (!string.IsNullOrWhiteSpace(goBackToCampSceneName))
            {
                SceneManager.LoadScene(goBackToCampSceneName);
                return;
            }

            ReloadActiveScene();
        }

        private static void ReloadActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
                return;
            }

            if (!string.IsNullOrWhiteSpace(activeScene.name))
            {
                SceneManager.LoadScene(activeScene.name);
                return;
            }

            Debug.LogError(
                $"{nameof(PlayerDefeatModalView)} could not reload the active scene because it has no valid build index or name."
            );
        }

        private void ApplyDisplayText()
        {
            if (titleText != null)
            {
                titleText.text = defeatedTitle;
            }

            if (messageText != null)
            {
                messageText.text = defeatedMessage;
            }

            if (restoreButtonText != null)
            {
                restoreButtonText.text = restoreCost > 0
                    ? $"{restoreButtonLabel}  {restoreCost}"
                    : restoreButtonLabel;
            }
        }

        private void PauseTimeIfConfigured()
        {
            if (!pauseGameTimeWhileModalIsOpen || hasPausedTimeForModal)
            {
                return;
            }

            timeScaleBeforeModal = Time.timeScale;
            Time.timeScale = 0f;
            hasPausedTimeForModal = true;
        }

        private void ResumeTimeIfPausedByModal()
        {
            if (!hasPausedTimeForModal)
            {
                return;
            }

            Time.timeScale = timeScaleBeforeModal;
            hasPausedTimeForModal = false;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (playerHealthController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerDefeatModalView)} on '{name}' is missing the " +
                    $"{nameof(PlayerHealthController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (modalRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerDefeatModalView)} on '{name}' is missing the Modal Root reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (goBackToCampButton == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerDefeatModalView)} on '{name}' is missing the Go Back To Camp Button reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (restoreAndKeepPlayingButton == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerDefeatModalView)} on '{name}' is missing the Restore And Keep Playing Button reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            if (defeatedTitle == null)
            {
                defeatedTitle = "YOU ARE DEAD";
            }

            if (defeatedMessage == null)
            {
                defeatedMessage = "Restore your character to keep playing or go back to camp.";
            }

            if (restoreButtonLabel == null)
            {
                restoreButtonLabel = "RESTORE & KEEP PLAYING";
            }

            restoreCost = Mathf.Max(0, restoreCost);
        }
    }
}

