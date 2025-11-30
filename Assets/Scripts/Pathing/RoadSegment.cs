using UnityEngine;
using UnityEngine.AI;

namespace Pathing
{
    [RequireComponent(typeof(NavMeshObstacle))]
    public class RoadSegment : MonoBehaviour
    {
        private NavMeshObstacle _obstacle;

        public void Initialize()
        {
            _obstacle = GetComponent<NavMeshObstacle>();
            _obstacle.carving = true;
            _obstacle.shape = NavMeshObstacleShape.Box;
        }

        public void SetWalkable(bool isWalkable)
        {
            // If it is walkable, we disable the obstacle (allowing pathfinding).
            // If it is NOT walkable, we enable the obstacle (blocking pathfinding).
            _obstacle.enabled = !isWalkable;
        }
    }
}