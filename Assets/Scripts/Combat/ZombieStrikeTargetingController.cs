using System;
using IsometricPathfinding.UI;
using IsometricPathfinding.Zombies;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IsometricPathfinding.Combat
{
    [DisallowMultipleComponent]
    public sealed class ZombieStrikeTargetingController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera worldCamera;

        [SerializeField] private DangerTurnController dangerTurnController;

        [SerializeField] private ZombieActionMenuView actionMenuView;

        [Header("Hover Detection")]
        [SerializeField] private LayerMask zombieLayerMask = Physics2D.DefaultRaycastLayers;

        [Header("Prompt Timing")]
        [SerializeField] [Min(0f)] private float hideDelay = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool logActionRequests;

        private ZombieAgent currentTarget;
        
        private ZombieHoverOutline currentOutline;

        private ZombieAttackOption selectedOption = ZombieAttackOption.None;

        private float hideTimer;

        public ZombieAgent CurrentTarget => currentTarget;

        public ZombieAttackOption SelectedOption => selectedOption;

        public event Action<ZombieAgent, ZombieAttackOption> ActionRequested;
        
        public bool IsTargetingZombieAction => currentTarget != null;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (actionMenuView != null)
            {
                actionMenuView.OptionSelected += OnOptionSelected;
            }
        }

        private void OnDisable()
        {
            if (actionMenuView != null)
            {
                actionMenuView.OptionSelected -= OnOptionSelected;
                actionMenuView.Hide();
            }

            currentTarget = null;
            selectedOption = ZombieAttackOption.None;
        }

        private void Update()
        {
            UpdateTargeting();
        }

        private void UpdateTargeting()
        {
            if (!CanUseZombieActions())
            {
                ClearCurrentTarget();
                return;
            }

            /*
             * If the mouse is over the UI menu, do not clear the target.
             * This allows the player to move from zombie body to Shoot/Strike buttons.
             */
            if (IsPointerOverUi())
            {
                KeepCurrentTargetIfValid();
                return;
            }

            bool hasHoveredZombie = TryGetHoveredZombie(out ZombieAgent hoveredZombie);

            /*
             * If the player already selected Shoot or Strike,
             * the menu is pinned to that zombie until the player clicks:
             *
             * - the same zombie: execute selected action
             * - elsewhere: cancel selected action
             */
            if (selectedOption != ZombieAttackOption.None)
            {
                UpdateSelectedActionClick(hasHoveredZombie, hoveredZombie);
                return;
            }

            /*
             * No option selected yet.
             * In this state, hovering a zombie only shows the menu.
             */
            if (!hasHoveredZombie)
            {
                RequestClearCurrentTarget();
                return;
            }

            if (!CanTargetZombie(hoveredZombie))
            {
                ClearCurrentTarget();
                return;
            }

            SetCurrentTarget(hoveredZombie, ZombieAttackOption.None);
        }

        private void UpdateSelectedActionClick(bool hasHoveredZombie, ZombieAgent hoveredZombie)
        {
            if (currentTarget == null)
            {
                ClearCurrentTarget();
                return;
            }

            if (!CanTargetZombie(currentTarget))
            {
                ClearCurrentTarget();
                return;
            }

            /*
             * Keep the menu visible while an action is selected.
             */
            actionMenuView.Show(currentTarget, selectedOption);

            if (Mouse.current == null)
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            /*
             * If player clicked the same zombie after choosing Shoot/Strike,
             * now we request the actual action.
             */
            if (hasHoveredZombie && hoveredZombie == currentTarget)
            {
                RequestSelectedAction();
                return;
            }

            /*
             * If player clicked somewhere else, cancel the chosen action.
             * This gives the player an easy way to back out.
             */
            ClearCurrentTarget();
        }

        private void RequestSelectedAction()
        {
            if (currentTarget == null)
            {
                ClearCurrentTarget();
                return;
            }

            if (selectedOption == ZombieAttackOption.None)
            {
                ClearCurrentTarget();
                return;
            }

            ZombieAgent target = currentTarget;
            ZombieAttackOption option = selectedOption;

            ClearCurrentTarget();

            if (logActionRequests)
            {
                Debug.Log($"{option} requested against {target.name}.", target);
            }

            ActionRequested?.Invoke(target, option);
        }

        private void OnOptionSelected(ZombieAttackOption option)
        {
            if (currentTarget == null)
            {
                selectedOption = ZombieAttackOption.None;
                actionMenuView.Hide();
                return;
            }

            if (!CanTargetZombie(currentTarget))
            {
                ClearCurrentTarget();
                return;
            }

            selectedOption = option;

            /*
             * Pin the menu to this target.
             * The next click on this zombie performs the selected action.
             */
            actionMenuView.Show(currentTarget, selectedOption);
        }

        private bool CanUseZombieActions()
        {
            if (dangerTurnController == null)
            {
                return false;
            }

            return dangerTurnController.CanPlayerAct();
        }

        private bool CanTargetZombie(ZombieAgent zombie)
        {
            if (zombie == null)
            {
                return false;
            }

            /*
             * For now, reuse your existing strike targeting rule:
             * - Danger Mode
             * - PlayerTurn
             * - zombie alive
             * - zombie active
             *
             * Later, if Shoot can target zombies from farther away or outside
             * melee range, you can split this into separate Strike/Shoot rules.
             */
            return dangerTurnController.CanStartStrike(zombie);
        }

        private void KeepCurrentTargetIfValid()
        {
            if (currentTarget == null)
            {
                return;
            }

            if (!CanTargetZombie(currentTarget))
            {
                ClearCurrentTarget();
                return;
            }

            hideTimer = hideDelay;
            
            ShowCurrentOutline();
            actionMenuView.Show(currentTarget, selectedOption);
        }

        private bool TryGetHoveredZombie(out ZombieAgent zombie)
        {
            zombie = null;

            if (Mouse.current == null)
            {
                return false;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

            if (!worldCamera.pixelRect.Contains(mouseScreenPosition))
            {
                return false;
            }

            Vector3 mouseWorldPosition = worldCamera.ScreenToWorldPoint(
                new Vector3(
                    mouseScreenPosition.x,
                    mouseScreenPosition.y,
                    Mathf.Abs(worldCamera.transform.position.z)
                )
            );

            Vector2 mouseWorldPoint = mouseWorldPosition;

            Collider2D hitCollider = Physics2D.OverlapPoint(
                mouseWorldPoint,
                zombieLayerMask
            );

            if (hitCollider == null)
            {
                return false;
            }

            zombie = hitCollider.GetComponentInParent<ZombieAgent>();

            return zombie != null;
        }

        private void SetCurrentTarget(ZombieAgent zombie, ZombieAttackOption option)
        {
            hideTimer = hideDelay;

            if (currentTarget != zombie)
            {
                HideCurrentOutline();

                currentTarget = zombie;
                currentOutline = currentTarget != null
                    ? currentTarget.GetComponentInChildren<ZombieHoverOutline>()
                    : null;
            }

            selectedOption = option;

            ShowCurrentOutline();

            actionMenuView.Show(currentTarget, selectedOption);
        }

        private void RequestClearCurrentTarget()
        {
            if (currentTarget == null)
            {
                return;
            }

            hideTimer -= Time.unscaledDeltaTime;

            if (hideTimer > 0f)
            {
                ShowCurrentOutline();
                actionMenuView.Show(currentTarget, selectedOption);
                return;
            }

            ClearCurrentTarget();
        }

        private void ClearCurrentTarget()
        {
            HideCurrentOutline();

            currentTarget = null;
            currentOutline = null;
            selectedOption = ZombieAttackOption.None;

            if (actionMenuView != null)
            {
                actionMenuView.Hide();
            }
        }
        
        private void ShowCurrentOutline()
        {
            if (currentOutline != null)
            {
                currentOutline.Show();
            }
        }

        private void HideCurrentOutline()
        {
            if (currentOutline != null)
            {
                currentOutline.Hide();
            }
        }

        private static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieStrikeTargetingController)} on '{name}' is missing the World Camera reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (dangerTurnController == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieStrikeTargetingController)} on '{name}' is missing the " +
                    $"{nameof(DangerTurnController)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            if (actionMenuView == null)
            {
                Debug.LogError(
                    $"{nameof(ZombieStrikeTargetingController)} on '{name}' is missing the " +
                    $"{nameof(ZombieActionMenuView)} reference.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }
    }
}