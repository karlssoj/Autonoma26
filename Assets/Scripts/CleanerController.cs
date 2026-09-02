using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;


public class CleanerController : Agent
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 180f;
 
    private Vector3 startingPosition;
    public GameObject Trash;
    private Quaternion startingRotation;

    private void Awake()
    {
        startingPosition = transform.position;
        startingRotation = transform.rotation;
    }


    public override void OnEpisodeBegin()
    {
        Reset();
    }

    public void Reset()
    {
        transform.SetPositionAndRotation(startingPosition, startingRotation);
        //Trash.GetComponent<TrashRespawn>().Respawn();

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    public void CollectTrash()
    {
        AddReward(1);
        EndEpisode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            AddReward(-1);
            EndEpisode();
        }
    }


    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0; // Movement action
        discreteActionsOut[1] = 0; // Rotation action

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            discreteActionsOut[0] = 1; // Move forward
        }

        if (Keyboard.current.aKey.isPressed)
        {
            discreteActionsOut[1] = 1; // Rotate left
        }
        else if (Keyboard.current.dKey.isPressed)
        {
            discreteActionsOut[1] = 2; // Rotate right
        }
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        int movementAction =actions.DiscreteActions[0];
        int rotationAction = actions.DiscreteActions[1];

        if (Keyboard.current == null)
        {
            return;
        }

        if (movementAction == 1) // Move forward
        {
            transform.Translate(
                Vector3.forward * moveSpeed * Time.deltaTime,
                Space.Self
            );
        }

        if (rotationAction == 1) // Rotate left  
        {
            transform.Rotate(
                Vector3.up * -rotationSpeed * Time.deltaTime,
                Space.Self
            );
        }
        else if (rotationAction == 2)
        {
            transform.Rotate(
                Vector3.up * rotationSpeed * Time.deltaTime,
                Space.Self
            );
        }    
    }

}
