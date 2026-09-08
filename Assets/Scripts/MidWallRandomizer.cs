using UnityEngine;

public class MidWallRandomizer : MonoBehaviour
{
    private Vector3 originalPosition;

    private void Awake()
    {
        // Spara väggens position från scenen.
        originalPosition = transform.position;
    }

    public void RandomizePosition()
    {
        // Slumpa endast horisontella koordinater runt ursprungspositionen.
        transform.position = new Vector3(
            originalPosition.x + Random.Range(-2f, 2f),
            originalPosition.y,
            originalPosition.z + Random.Range(-1f, 1f)
        );
    }
}
