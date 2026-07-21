using System;
using IsometricPathfinding.Movement;
using UnityEngine;

namespace IsometricPathfinding.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class TurnBasedCameraTarget : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField]
        private PlayerGridMover playerGridMover;

        [Header("Target Settings")]
        [SerializeField]
        private Vector2 targetOffset;

        [Header("Runtime State")]
        [SerializeField]
        private Vector3 lastFocusedPosition;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (playerGridMover == null)
            {
                return;
            }

            playerGridMover.MovementCompleted += HandleMovementCompleted;
        }

        private void Start()
        {
            SnapToPlayer();
        }

        private void OnDisable()
        {
            if (playerGridMover == null)
            {
                return;
            }

            playerGridMover.MovementCompleted -= HandleMovementCompleted;
        }

        private void HandleMovementCompleted(object sender, EventArgs e)
        {
            SnapToPlayer();
        }

        [ContextMenu("Snap Target To Player")]
        public void SnapToPlayer()
        {
            if (playerGridMover == null)
            {
                return;
            }

            Vector3 playerPosition = playerGridMover.transform.position;

            Vector3 newTargetPosition = new Vector3(
                playerPosition.x + targetOffset.x,
                playerPosition.y + targetOffset.y,
                transform.position.z
            );

            transform.position = newTargetPosition;

            lastFocusedPosition = newTargetPosition;
        }

        private bool ValidateReferences()
        {
            if (playerGridMover != null)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(TurnBasedCameraTarget)} on "
                    + $"'{name}' is missing the "
                    + "Player Grid Mover reference.",
                this
            );

            return false;
        }
    }
}
