using UnityEngine;

public class FrontDocking : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.name == "ChargeConnector")
        {
            GetComponentInParent<CleanerController>().Docked();
        }
    }
}
