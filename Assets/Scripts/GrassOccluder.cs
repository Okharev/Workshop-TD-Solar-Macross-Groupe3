using UnityEngine;

public class GrassOccluder : MonoBehaviour
{
    public float radius = 3.0f; // Rayon de la zone sans herbe

    private void OnEnable()
    {
        GrassRenderer.RegisterOccluder(this);
    }

    private void OnDisable()
    {
        GrassRenderer.UnregisterOccluder(this);
    }

    // Gizmo pour voir la zone dans l'éditeur
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}