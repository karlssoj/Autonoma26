using System.Collections.Generic;
using UnityEngine;

public class MidWallRandomizer : MonoBehaviour
{
    // Referenser och marginaler för den aktuella miljön.
    [SerializeField] private Collider floorCollider;
    [SerializeField] private Transform cleaner;
    [SerializeField] private Transform trash;
    [SerializeField] private float minimumWallGap = 1.5f;
    [SerializeField] private float protectedObjectGap = 1.5f;
    [SerializeField] private int maxAttemptsPerWall = 100;

    private static readonly List<MidWallRandomizer> instances = new();
    private readonly List<Transform> midWalls = new();
    private Transform environmentRoot;

    private void Awake()
    {
        // Hitta miljöns golv, robot, trash och mellanväggar.
        environmentRoot = FindEnvironmentRoot();
        cleaner = transform;

        FindMidWalls();

        if (floorCollider == null)
        {
            floorCollider = FindChildCollider("Floor");
        }

        if (cleaner == null)
        {
            cleaner = FindChildTransform("VacumCleaner");
        }

        if (trash == null)
        {
            trash = FindChildTransform("Trash");
        }
    }

    private void OnDestroy()
    {
        instances.Remove(this);
    }

    public void RandomizeEnvironment()
    {
        // Slumpa om alla mellanväggar när en episode börjar.
        if (midWalls.Count == 0 || floorCollider == null)
        {
            Debug.LogError("MidWallRandomizer needs a floor collider and at least one MidWall.", this);
            return;
        }

        Bounds floorBounds = floorCollider.bounds;
        Dictionary<Transform, Vector3> oldPositions = new();
        Dictionary<Transform, Quaternion> oldRotations = new();

        foreach (Transform wall in midWalls)
        {
            oldPositions[wall] = wall.transform.position;
            oldRotations[wall] = wall.transform.rotation;
        }

        for (int wallIndex = 0; wallIndex < midWalls.Count; wallIndex++)
        {
            // Försök hitta en giltig position för aktuell mellanvägg.
            Transform wall = midWalls[wallIndex];
            bool foundPosition = false;

            for (int attempt = 0; attempt < maxAttemptsPerWall; attempt++)
            {
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                wall.transform.rotation = rotation;
                Physics.SyncTransforms();
                Vector3 position = RandomPositionInsideFloor(wall, floorBounds);
                wall.transform.position = position;
                Physics.SyncTransforms();

                if (IsValidPosition(wall, floorBounds))
                {
                    // Behåll kandidaten när alla avståndskrav är uppfyllda.
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                // Återställ hela layouten om en giltig layout inte hittas.
                foreach (Transform oldWall in midWalls)
                {
                    oldWall.transform.SetPositionAndRotation(oldPositions[oldWall], oldRotations[oldWall]);
                }

                Physics.SyncTransforms();
                Debug.LogWarning("Could not find a valid random position for the MidWall layout.", this);
                return;
            }
        }
    }

    private bool IsValidPosition(Transform wall, Bounds floorBounds)
    {
        // Kontrollera golvgränser, skyddsavstånd och väggavstånd.
        Collider wallCollider = wall.GetComponent<Collider>();
        if (wallCollider == null || !IsInsideFloor(wallCollider, floorBounds))
        {
            return false;
        }

        float requiredWallGap = GetRequiredWallGap();

        if (OverlapsProtectedObject(wallCollider, cleaner) ||
            OverlapsProtectedObject(wallCollider, trash))
        {
            return false;
        }

        foreach (Transform otherWall in midWalls)
        {
            if (otherWall == wall)
            {
                continue;
            }

            Collider otherCollider = otherWall.GetComponent<Collider>();
            if (otherCollider != null && HasPenetration(wallCollider, otherCollider, requiredWallGap))
            {
                return false;
            }
        }

        foreach (Collider fixedWallCollider in environmentRoot.GetComponentsInChildren<Collider>())
        {
            if (fixedWallCollider.transform == wall ||
                fixedWallCollider.transform.name == "MidWall" ||
                !fixedWallCollider.CompareTag("wall"))
            {
                continue;
            }

            if (HasPenetration(wallCollider, fixedWallCollider, requiredWallGap))
            {
                return false;
            }
        }

        return true;
    }

    private float GetRequiredWallGap()
    {
        // Säkerställ minst robotens bredd mellan två väggar.
        float cleanerDiameter = 0f;

        if (cleaner != null)
        {
            foreach (Collider cleanerCollider in cleaner.GetComponentsInChildren<Collider>())
            {
                Vector3 size = cleanerCollider.bounds.size;
                cleanerDiameter = Mathf.Max(cleanerDiameter, size.x, size.z);
            }
        }

        return Mathf.Max(minimumWallGap, cleanerDiameter);
    }

    private bool IsInsideFloor(Collider wallCollider, Bounds floorBounds)
    {
        // Kontrollera att hela mellanväggen ryms på golvet.
        Bounds wallBounds = wallCollider.bounds;
        return wallBounds.min.x >= floorBounds.min.x &&
               wallBounds.max.x <= floorBounds.max.x &&
               wallBounds.min.z >= floorBounds.min.z &&
               wallBounds.max.z <= floorBounds.max.z;
    }

    private bool OverlapsProtectedObject(Collider wallCollider, Transform protectedObject)
    {
        // Hindra väggen från att hamna nära robot eller trash.
        if (protectedObject == null)
        {
            return false;
        }

        foreach (Collider objectCollider in protectedObject.GetComponentsInChildren<Collider>())
        {
            if (HasPenetration(wallCollider, objectCollider, protectedObjectGap))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPenetration(Collider first, Collider second, float extraGap)
    {
        // Använd utökade bounds för att kontrollera säkerhetsmarginal.
        Bounds firstBounds = first.bounds;
        firstBounds.Expand(extraGap * 2f);
        return firstBounds.Intersects(second.bounds);
    }

    private Vector3 RandomPositionInsideFloor(Transform wall, Bounds floorBounds)
    {
        // Beräkna en slumpad position som ryms innanför golvet.
        Collider wallCollider = wall.GetComponent<Collider>();
        Vector3 halfExtents = wallCollider.bounds.extents;
        float x = Random.Range(floorBounds.min.x + halfExtents.x, floorBounds.max.x - halfExtents.x);
        float z = Random.Range(floorBounds.min.z + halfExtents.z, floorBounds.max.z - halfExtents.z);
        float localBottomOffset = wallCollider.bounds.min.y - wall.position.y;
        float y = floorBounds.max.y - localBottomOffset;
        return new Vector3(x, y, z);
    }

    private void FindMidWalls()
    {
        // Hitta alla mellanväggar i denna miljö.
        midWalls.Clear();
        foreach (Transform child in environmentRoot.GetComponentsInChildren<Transform>())
        {
            if (child.name == "MidWall" && child.GetComponent<Collider>() != null)
            {
                midWalls.Add(child);
            }
        }
    }

    private Collider FindChildCollider(string childName)
    {
        foreach (Collider childCollider in environmentRoot.GetComponentsInChildren<Collider>())
        {
            if (childCollider.name == childName)
            {
                return childCollider;
            }
        }

        return null;
    }

    private Transform FindChildTransform(string childName)
    {
        foreach (Transform childTransform in environmentRoot.GetComponentsInChildren<Transform>())
        {
            if (childTransform.name == childName)
            {
                return childTransform;
            }
        }

        return null;
    }

    private Transform FindEnvironmentRoot()
    {
        // Leta upp närmaste förälder som representerar en hel miljö.
        Transform candidate = transform;

        while (candidate.parent != null)
        {
            candidate = candidate.parent;

            bool hasFloor = false;
            bool hasTrash = false;
            bool hasMidWall = false;

            foreach (Transform child in candidate.GetComponentsInChildren<Transform>())
            {
                hasFloor |= child.name == "Floor";
                hasTrash |= child.name == "Trash";
                hasMidWall |= child.name == "MidWall";
            }

            if (hasFloor && hasTrash && hasMidWall)
            {
                return candidate;
            }
        }

        return transform;
    }
}