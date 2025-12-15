using UnityEngine;

public class WindFollowCamera : MonoBehaviour
{
    [Header("Cible")]
    public Transform targetToFollow;

    [Header("Réglages")]
    public Vector3 offset = new Vector3(0f, 3f, 0f); // Hauteur par défaut
    public float lookAheadDistance = 15f; // Distance devant la caméra

    void LateUpdate()
    {
        if (!targetToFollow)
        {
            if (UnityEngine.Camera.main) targetToFollow = UnityEngine.Camera.main.transform;
            else return;
        }

        Vector3 targetPos = targetToFollow.position;

        // Projection vers l'avant (sur le plan horizontal)
        Vector3 forwardFlat = targetToFollow.forward;
        forwardFlat.y = 0; 
        
        targetPos += forwardFlat.normalized * lookAheadDistance;

        // Application position + offset
        transform.position = targetPos + offset;
        
        // Pas de rotation ! La boite reste alignée au monde.
    }
}