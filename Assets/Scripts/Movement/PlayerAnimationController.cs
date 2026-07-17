using UnityEngine;

namespace IsometricPathfinding.Movement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private const string IsMovingParameterName = "IsMoving";

        private const string FacingDirectionParameterName = "FacingDirection";

        private static readonly int IsMovingParameterHash = Animator.StringToHash(
            IsMovingParameterName
        );

        private static readonly int FacingDirectionParameterHash = Animator.StringToHash(
            FacingDirectionParameterName
        );

        [Header("Scene References")]
        [SerializeField]
        private PlayerGridMover playerGridMover;

        [SerializeField]
        private Animator animator;

        [Header("Runtime State")]
        [SerializeField]
        private bool appliedIsMoving;

        [SerializeField]
        private GridDirection appliedFacingDirection = GridDirection.Down;

        private bool hasAppliedState;

        private void Reset()
        {
            animator = GetComponent<Animator>();

            playerGridMover = GetComponentInParent<PlayerGridMover>();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (playerGridMover == null)
            {
                playerGridMover = GetComponentInParent<PlayerGridMover>();
            }

            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            if (!ValidateAnimatorParameters())
            {
                enabled = false;
            }
        }

        private void Start()
        {
            ApplyAnimationParameters(forceUpdate: true);
        }

        private void LateUpdate()
        {
            ApplyAnimationParameters(forceUpdate: false);
        }

        private void ApplyAnimationParameters(bool forceUpdate)
        {
            bool newIsMoving = playerGridMover.IsMoving;

            GridDirection newFacingDirection = playerGridMover.FacingDirection;

            /*
             * FacingDirection bi tijekom normalne igre
             * trebao uvijek sadržavati posljednji valjani
             * smjer.
             *
             * Ipak osiguravamo sigurnu početnu vrijednost.
             */

            if (newFacingDirection == GridDirection.None)
            {
                newFacingDirection = GridDirection.Down;
            }

            bool stateHasNotChanged =
                hasAppliedState
                && appliedIsMoving == newIsMoving
                && appliedFacingDirection == newFacingDirection;

            if (!forceUpdate && stateHasNotChanged)
            {
                return;
            }

            animator.SetBool(IsMovingParameterHash, newIsMoving);

            animator.SetInteger(FacingDirectionParameterHash, (int)newFacingDirection);

            appliedIsMoving = newIsMoving;

            appliedFacingDirection = newFacingDirection;

            hasAppliedState = true;
        }

        private bool ValidateReferences()
        {
            bool referencesAreValid = true;

            if (playerGridMover == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerAnimationController)} "
                        + $"on '{name}' could not find a "
                        + $"{nameof(PlayerGridMover)} in its "
                        + "parent hierarchy.",
                    this
                );

                referencesAreValid = false;
            }

            if (animator == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerAnimationController)} "
                        + $"on '{name}' is missing its Animator.",
                    this
                );

                referencesAreValid = false;
            }
            else if (animator.runtimeAnimatorController == null)
            {
                Debug.LogError(
                    $"Animator on '{name}' does not have " + "a Runtime Animator Controller.",
                    this
                );

                referencesAreValid = false;
            }

            return referencesAreValid;
        }

        private bool ValidateAnimatorParameters()
        {
            bool hasIsMovingParameter = false;

            bool hasFacingDirectionParameter = false;

            AnimatorControllerParameter[] parameters = animator.parameters;

            foreach (AnimatorControllerParameter parameter in parameters)
            {
                if (
                    parameter.nameHash == IsMovingParameterHash
                    && parameter.type == AnimatorControllerParameterType.Bool
                )
                {
                    hasIsMovingParameter = true;
                }

                if (
                    parameter.nameHash == FacingDirectionParameterHash
                    && parameter.type == AnimatorControllerParameterType.Int
                )
                {
                    hasFacingDirectionParameter = true;
                }
            }

            if (!hasIsMovingParameter)
            {
                Debug.LogError(
                    $"Animator Controller on '{name}' "
                        + $"must contain a Bool parameter named "
                        + $"'{IsMovingParameterName}'.",
                    this
                );
            }

            if (!hasFacingDirectionParameter)
            {
                Debug.LogError(
                    $"Animator Controller on '{name}' "
                        + $"must contain an Int parameter named "
                        + $"'{FacingDirectionParameterName}'.",
                    this
                );
            }

            return hasIsMovingParameter && hasFacingDirectionParameter;
        }
    }
}
