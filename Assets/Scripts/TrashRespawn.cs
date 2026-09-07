using UnityEngine;
using System;

public class TrashRespawn : MonoBehaviour
{
    // Golvets collider används som område för nya spawnpositioner.
    [SerializeField] private Collider floorCollider;
    private System.Random randomGenerator;

    public void Respawn()
    {
        // Kontrollera att trash och golv har nödvändiga komponenter.
        if (floorCollider == null)
        {
            Debug.LogError("TrashRespawn needs a floor collider assigned.", this);
            return;
        }

        Collider trashCollider = GetComponent<Collider>();
        if (trashCollider == null)
        {
            Debug.LogError("TrashRespawn needs a collider on the trash object.", this);
            return;
        }

        // Skapa en ny slumpgenerator för denna respawn.
        randomGenerator = new System.Random(this.GetHashCode() ^ (int)(Time.time * 10000));

        Bounds floorBounds = floorCollider.bounds;
        Vector3 originalPosition = transform.position;
        Quaternion originalRotation = transform.rotation;
        Rigidbody body = GetComponent<Rigidbody>();

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        for (int attempt = 0; attempt < 20; attempt++)
        {
            // Välj en slumpmässig punkt och kasta en stråle mot golvet.
            Vector3 rayStart = new Vector3(
                Mathf.Lerp(floorBounds.min.x, floorBounds.max.x, (float)randomGenerator.NextDouble()),
                floorBounds.max.y + 10f,
                Mathf.Lerp(floorBounds.min.z, floorBounds.max.z, (float)randomGenerator.NextDouble())
            );

            if (floorCollider.Raycast(new Ray(rayStart, Vector3.down), out RaycastHit hit, 100f))
            {
                // Placera trash på golvet med slumpad rotation.
                transform.rotation = Quaternion.Euler(0f, (float)randomGenerator.NextDouble() * 360f, 0f);
                transform.position = hit.point;

                float lift = hit.point.y - trashCollider.bounds.min.y;
                transform.position += Vector3.up * lift;
                Physics.SyncTransforms();

                if (!OverlapsWall(trashCollider))
                {
                    // Godkänn bara positioner som inte ligger i en vägg.
                    return;
                }
            }
        }

        transform.SetPositionAndRotation(originalPosition, originalRotation);
        Physics.SyncTransforms();
        Debug.LogWarning("Could not find a valid random position on the floor.", this);
    }

    private bool OverlapsWall(Collider trashCollider)
    {
        // Leta efter väggar nära trash och kontrollera faktisk penetration.
        Collider[] nearbyColliders = Physics.OverlapBox(
            trashCollider.bounds.center,
            trashCollider.bounds.extents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            if (nearbyCollider == trashCollider || !nearbyCollider.CompareTag("wall"))
            {
                continue;
            }

            if (Physics.ComputePenetration(
                    trashCollider,
                    transform.position,
                    transform.rotation,
                    nearbyCollider,
                    nearbyCollider.transform.position,
                    nearbyCollider.transform.rotation,
                    out _,
                    out float penetrationDistance) && penetrationDistance > 0.0001f)
            {
                return true;
            }
        }

        return false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Samla in trash eller flytta det om det träffar en vägg.
        CleanerController cleaner = collision.gameObject.GetComponentInParent<CleanerController>();
        if (cleaner != null)
        {
            cleaner.CollectTrash();
        }

        if(collision.gameObject.CompareTag("wall"))
        {
            Debug.Log("Trash collided with wall, respawning.");
            Respawn();
        }
    }
}
