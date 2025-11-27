using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TestNavMesh : MonoBehaviour
{
    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        var dest = GameObject.FindGameObjectWithTag("Nexus");
        _agent.SetDestination(dest.transform.position);
    }
}