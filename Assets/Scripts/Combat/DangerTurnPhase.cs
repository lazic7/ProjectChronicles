namespace IsometricPathfinding.Combat
{
    public enum DangerTurnPhase
    {
        None = 0,

        /*
         * The player can choose an action:
         * - move normally
         * - click Strike
         * - later maybe use items, shoot, interact, etc.
         */
        PlayerTurn = 1,

        /*
         * The player clicked Strike, but is not adjacent yet.
         * During this phase the player is walking to an adjacent tile.
         *
         * Important:
         * When this movement completes, we do NOT want to start the zombie turn.
         * Instead, we want to open the strike minigame.
         */
        PlayerStrikeApproach = 2,

        /*
         * The timing minigame is active.
         * Gameplay will be paused with Time.timeScale = 0,
         * but the minigame UI will still animate with Time.unscaledDeltaTime.
         */
        StrikeMinigame = 3,

        /*
         * Zombies are currently taking their turn.
         */
        ZombieTurn = 4
    }
}