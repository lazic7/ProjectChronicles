using IsometricPathfinding.Combat;
using IsometricPathfinding.Zombies;
using TMPro;
using UnityEngine;

namespace IsometricPathfinding.UI
{
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackLabelView : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Canvas canvas;

        [SerializeField] private Camera worldCamera;

        [SerializeField] private DangerTurnController dangerTurnController;

        [SerializeField] private Transform playerTarget;

        [SerializeField] private RectTransform labelRoot;

        [SerializeField] private TextMeshProUGUI labelText;

        [Header("World Positioning")]
        [SerializeField] private Vector3 zombieWorldOffset = new Vector3(0f, 1.25f, 0f);

        [SerializeField] private Vector3 playerWorldOffset = new Vector3(0f, 1.25f, 0f);

        [SerializeField] private Vector2 screenOffset;

        [Header("Display Timing")]
        [SerializeField] [Min(0.1f)] private float displayDuration = 1.25f;

        [Header("Messages")]
        [SerializeField] private string zombieMissedMessage = "MISS!";

        [SerializeField] private string playerMissedMessage = "MISS!";

        [SerializeField] private string playerShootMissedMessage = "MISS!";

        [SerializeField] private string zombieKilledMessage = "HIT!";

        [Header("Colors")]
        [SerializeField] private Color zombieMissedColor = new Color(1f, 1f, 1f, 1f);

        [SerializeField] private Color playerMissedColor = new Color(1f, 1f, 1f, 1f);

        [SerializeField] private Color playerShootMissedColor = new Color(1f, 1f, 1f, 1f);

        [SerializeField] private Color zombieKilledColor = new Color(1f, 1f, 1f, 1f);

        [Header("Debug")]
        [SerializeField] private bool logFeedbackEvents;

        private GameObject labelRootObject;

        private RectTransform canvasRectTransform;

        private Camera uiCamera;

        private Vector2 defaultAnchoredPosition;

        private Transform currentWorldTarget;

        private Vector3 currentWorldOffset;

        private float hideTimer;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            labelRootObject = labelRoot.gameObject;
            canvasRectTransform = canvas.transform as RectTransform;
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            defaultAnchoredPosition = labelRoot.anchoredPosition;

            HideImmediately();
        }

        private void OnEnable()
        {
            if (dangerTurnController != null)
            {
                dangerTurnController.PlayerStrikeMissed += OnPlayerStrikeMissed;
                dangerTurnController.PlayerShootMissed += OnPlayerShootMissed;
                dangerTurnController.PlayerKilledZombie += OnPlayerKilledZombie;
            }

            ZombieAgent.ZombieAttackMissedPlayer += OnZombieAttackMissedPlayer;
        }

        private void OnDisable()
        {
            if (dangerTurnController != null)
            {
                dangerTurnController.PlayerStrikeMissed -= OnPlayerStrikeMissed;
                dangerTurnController.PlayerShootMissed -= OnPlayerShootMissed;
                dangerTurnController.PlayerKilledZombie -= OnPlayerKilledZombie;
            }

            ZombieAgent.ZombieAttackMissedPlayer -= OnZombieAttackMissedPlayer;
        }

        private void Update()
        {
            if (hideTimer <= 0f)
            {
                return;
            }

            if (currentWorldTarget != null)
            {
                UpdateLabelWorldPosition(currentWorldTarget, currentWorldOffset);
            }

            hideTimer -= Time.unscaledDeltaTime;

            if (hideTimer <= 0f)
            {
                HideImmediately();
            }
        }

        private void OnZombieAttackMissedPlayer(ZombieAgent zombie)
        {
            if (logFeedbackEvents)
            {
                Debug.Log(
                    zombie == null
                        ? "Combat feedback received: zombie missed player, but zombie reference was null."
                        : $"Combat feedback received: {zombie.name} missed player.",
                    this
                );
            }

            ShowMessageOverTransform(zombieMissedMessage, zombieMissedColor, playerTarget, playerWorldOffset);
        }

        private void OnPlayerStrikeMissed(ZombieAgent zombie)
        {
            if (logFeedbackEvents)
            {
                Debug.Log(
                    zombie == null
                        ? "Combat feedback received: player missed, but zombie reference was null."
                        : $"Combat feedback received: player missed {zombie.name}.",
                    this
                );
            }

            ShowMessageOverZombie(playerMissedMessage, playerMissedColor, zombie);
        }

        private void OnPlayerShootMissed(ZombieAgent zombie)
        {
            if (logFeedbackEvents)
            {
                Debug.Log(
                    zombie == null
                        ? "Combat feedback received: player shot missed, but zombie reference was null."
                        : $"Combat feedback received: player shot missed {zombie.name}.",
                    this
                );
            }

            ShowMessageOverZombie(playerShootMissedMessage, playerShootMissedColor, zombie);
        }

        private void OnPlayerKilledZombie(ZombieAgent zombie)
        {
            if (logFeedbackEvents)
            {
                Debug.Log(
                    zombie == null
                        ? "Combat feedback received: zombie killed, but zombie reference was null."
                        : $"Combat feedback received: player killed {zombie.name}.",
                    this
                );
            }

            ShowMessageOverZombie(zombieKilledMessage, zombieKilledColor, zombie);
        }

        private void ShowMessageOverZombie(string message, Color color, ZombieAgent zombie)
        {
            Transform targetTransform = zombie == null
                ? null
                : zombie.transform;

            ShowMessageOverTransform(message, color, targetTransform, zombieWorldOffset);
        }

        private void ShowMessageOverTransform(string message, Color color, Transform target, Vector3 worldOffset)
        {
            currentWorldTarget = target;
            currentWorldOffset = worldOffset;

            if (labelRootObject == null || labelText == null)
            {
                return;
            }

            ShowMessageInternal(message, color);

            if (currentWorldTarget != null)
            {
                UpdateLabelWorldPosition(currentWorldTarget, currentWorldOffset);
                return;
            }

            labelRoot.anchoredPosition = defaultAnchoredPosition;
        }

        private void ShowMessageInternal(string message, Color color)
        {
            labelText.text = message;
            labelText.color = color;

            if (!labelRootObject.activeSelf)
            {
                labelRootObject.SetActive(true);
            }

            hideTimer = displayDuration;
        }

        private void UpdateLabelWorldPosition(Transform worldTarget, Vector3 worldOffset)
        {
            if (worldTarget == null || canvasRectTransform == null)
            {
                return;
            }

            Vector3 worldPosition = worldTarget.position + worldOffset;

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

            labelRoot.anchoredPosition = localPoint + screenOffset;
        }

        private void HideImmediately()
        {
            hideTimer = 0f;
            currentWorldTarget = null;
            currentWorldOffset = Vector3.zero;

            if (labelRootObject != null)
            {
                labelRootObject.SetActive(false);
            }
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (canvas == null)
            {
                Debug.LogError(
                    $"{nameof(CombatFeedbackLabelView)} on '{name}' is missing the Canvas reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(CombatFeedbackLabelView)} on '{name}' is missing the World Camera reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (dangerTurnController == null)
            {
                Debug.LogError(
                    $"{nameof(CombatFeedbackLabelView)} on '{name}' is missing the " +
                    $"{nameof(DangerTurnController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (playerTarget == null)
            {
                Debug.LogError(
                    $"{nameof(CombatFeedbackLabelView)} on '{name}' is missing the Player Target reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (labelRoot == null)
            {
                Debug.LogError(
                    $"{nameof(CombatFeedbackLabelView)} on '{name}' is missing the Label Root reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (labelText == null)
            {
                Debug.LogError(
                    $"{nameof(CombatFeedbackLabelView)} on '{name}' is missing the TextMeshProUGUI reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private void OnValidate()
        {
            displayDuration = Mathf.Max(0.1f, displayDuration);

            if (zombieMissedMessage == null)
            {
                zombieMissedMessage = "Zombie missed!";
            }

            if (playerMissedMessage == null)
            {
                playerMissedMessage = "You missed!";
            }

            if (zombieKilledMessage == null)
            {
                zombieKilledMessage = "Zombie killed!";
            }

            if (playerShootMissedMessage == null)
            {
                playerShootMissedMessage = "You missed!";
            }
        }
    }
}