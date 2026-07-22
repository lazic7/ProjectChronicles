using System.Collections.Generic;
using UnityEngine;

namespace IsometricPathfinding.Navigation
{
    [DisallowMultipleComponent]
    public sealed class GridOccupancyManager : MonoBehaviour
    {
        private readonly Dictionary<Vector2Int, GameObject> occupants = new Dictionary<Vector2Int, GameObject>();

        public bool IsOccupied(Vector2Int cell)
        {
            return occupants.ContainsKey(cell);
        }

        public bool IsOccupiedByOther(Vector2Int cell, GameObject actor)
        {
            if (!occupants.TryGetValue(cell, out GameObject occupant))
            {
                return false;
            }

            return occupant != actor;
        }

        public bool TryGetOccupant(Vector2Int cell, out GameObject actor)
        {
            return occupants.TryGetValue(cell, out actor);
        }

        public bool TryRegister(GameObject actor, Vector2Int cell)
        {
            if (actor == null)
            {
                return false;
            }

            if (occupants.TryGetValue(cell, out GameObject existingOccupant))
            {
                if (existingOccupant == actor)
                {
                    return false;
                }
                
                Debug.LogWarning($"Cannot register '{actor.name}' at {cell}. " + $"Cell is already occupied by '{existingOccupant.name}'.", actor);

                return false;
            }
            
            occupants.Add(cell, actor);
            return true;
        }
        
        public void Unregister(GameObject actor, Vector2Int cell)
        {
            if (actor == null)
            {
                return;
            }

            if (!occupants.TryGetValue(cell, out GameObject existingOccupant))
            {
                return;
            }

            if (existingOccupant != actor)
            {
                return;
            }

            occupants.Remove(cell);
        }
        
        public bool TryMove(GameObject actor, Vector2Int fromCell, Vector2Int toCell)
        {
            if (actor == null)
            {
                return false;
            }

            if (fromCell == toCell)
            {
                return true;
            }

            if (occupants.TryGetValue(toCell, out GameObject existingOccupant)
                && existingOccupant != actor)
            {
                Debug.LogWarning($"Cannot move '{actor.name}' from {fromCell} to {toCell}. " + $"Destination is occupied by '{existingOccupant.name}'.", actor);

                return false;
            }

            if (occupants.TryGetValue(fromCell, out GameObject currentOccupant)
                && currentOccupant == actor)
            {
                occupants.Remove(fromCell);
            }

            occupants[toCell] = actor;
            return true;
        }

        public void Clear()
        {
            occupants.Clear();
        }
    }
}
