using UnityEngine;

public class PointTerrainPlacement : MonoBehaviour
{
    public Terrain terrain;
    [Min(0.0f)]
    public float surfaceOffset = 0.5f;

    private Collider[] pointColliders;

    private void Awake()
    {
        pointColliders = GetComponentsInChildren<Collider>();

        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }
    }

    public void PlaceAboveTerrain()
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogWarning("PointTerrainPlacement behöver en Terrain med TerrainData.", this);
            return;
        }

        Vector3 position = transform.position;
        float terrainHeight = GetTerrainHeight(position);
        float lowestPoint = float.PositiveInfinity;

        foreach (Collider pointCollider in pointColliders)
        {
            lowestPoint = Mathf.Min(lowestPoint, pointCollider.bounds.min.y);
        }

        if (float.IsPositiveInfinity(lowestPoint))
        {
            position.y = terrainHeight + surfaceOffset;
        }
        else
        {
            position.y += terrainHeight + surfaceOffset - lowestPoint;
        }

        transform.position = position;
    }

    private float GetTerrainHeight(Vector3 worldPosition)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 localPosition = terrain.transform.InverseTransformPoint(worldPosition);
        Vector3 terrainSize = terrainData.size;
        float normalizedX = Mathf.Clamp01(localPosition.x / terrainSize.x);
        float normalizedZ = Mathf.Clamp01(localPosition.z / terrainSize.z);
        float localHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);

        return terrain.transform.TransformPoint(0.0f, localHeight, 0.0f).y;
    }
}