using UnityEngine;

public class PlayerParticleController : MonoBehaviour
{
    [SerializeField] private ParticleSystem movementParticles;

    private Vector2 lastPosition;

    private ParticleSystem.EmissionModule particleEmissionModule;

    private void Start()
    {
        lastPosition = transform.position;

        if (movementParticles != null) particleEmissionModule = movementParticles.emission;
        particleEmissionModule.enabled = false;
    }

    private void LateUpdate()
    {
        float distanceMoved = Vector2.Distance(lastPosition, transform.position);

        bool isMoving = distanceMoved > 0.01f;

        if (isMoving != particleEmissionModule.enabled) particleEmissionModule.enabled = isMoving;

        lastPosition = transform.position;
    }


}