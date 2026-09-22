using UnityEngine;

[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(TerrainCollider))]
public class RandomTerrainGenerator : MonoBehaviour
{
    [Range(0.0f, 1.0f)]
    [Tooltip("Hur starka kullarna och dalarna blir.")]
    public float hillIntensity = 0.5f;

    [Min(0.0f)]
    [Tooltip("Maximal höjdskillnad från den befintliga terrängen i meter.")]
    public float maximumHillHeight = 2.0f;

    private const float NoiseScale = 3.0f;
    private Terrain terrain;
    private TerrainCollider terrainCollider;
    private TerrainData generatedTerrainData;
    private float[,] baseHeights;
    private Vector2 noiseOffset;

    private void Awake()
    {
        terrain = GetComponent<Terrain>();
        terrainCollider = GetComponent<TerrainCollider>();
    }

    private void Start()
    {
        GenerateRandomTerrain();
    }

    [ContextMenu("Generate Random Terrain")]
    public void GenerateRandomTerrain()
    {
        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }

        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogWarning("RandomTerrainGenerator behöver ett Terrain med TerrainData.", this);
            return;
        }

        PrepareRuntimeTerrainData();

        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        float[,] heights = (float[,])baseHeights.Clone();
        float heightScale = maximumHillHeight / Mathf.Max(terrainData.size.y, 0.0001f);
        float intensity = Mathf.Clamp01(hillIntensity);

        noiseOffset = new Vector2(
            Random.Range(0.0f, 10000.0f),
            Random.Range(0.0f, 10000.0f));

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = (float)x / (resolution - 1);
                float normalizedY = (float)y / (resolution - 1);
                float noise = Mathf.PerlinNoise(
                    normalizedX * NoiseScale + noiseOffset.x,
                    normalizedY * NoiseScale + noiseOffset.y);
                float signedNoise = (noise * 2.0f) - 1.0f;

                heights[y, x] = Mathf.Clamp01(
                    heights[y, x] + signedNoise * heightScale * intensity);
            }
        }

        terrainData.SetHeights(0, 0, heights);
        terrainCollider.terrainData = terrainData;
    }

    private void PrepareRuntimeTerrainData()
    {
        if (generatedTerrainData != null && terrain.terrainData == generatedTerrainData)
        {
            return;
        }

        generatedTerrainData = Instantiate(terrain.terrainData);
        generatedTerrainData.name = terrain.terrainData.name + " (Runtime)";
        terrain.terrainData = generatedTerrainData;
        terrainCollider.terrainData = generatedTerrainData;
        baseHeights = generatedTerrainData.GetHeights(
            0,
            0,
            generatedTerrainData.heightmapResolution,
            generatedTerrainData.heightmapResolution);
    }
}