using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;
using UnityEngine.InputSystem;


public class CleanerController : Agent
{
    // Inställningar för robotens rörelse.
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 180f;
 
    // Referenser till robotens startläge och miljö.
    private Vector3 startingPosition;
    public GameObject Trash;
    private Quaternion startingRotation;
    private Rigidbody body;

    private void Awake()
    {
        // Spara startläget och hitta fysikkomponenten.
        startingPosition = transform.position;
        startingRotation = transform.rotation;
        body = GetComponent<Rigidbody>();
        MaxStep = 1000;
    }


    public override void OnEpisodeBegin()
    {
        // Återställ miljön när ett nytt träningsavsnitt börjar.
        Reset();
    }

    public void Reset()
    {
        // Flytta tillbaka roboten och skapa en ny vägglayout.
        transform.SetPositionAndRotation(startingPosition, startingRotation);

        foreach (MidWallRandomizer midWall in FindObjectsOfType<MidWallRandomizer>())
        {
            midWall.RandomizePosition();
        }
    
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

    }

    public void CollectTrash()
    {
        // Belöna agenten när den hittar skräpet.
        AddReward(1);
        EndEpisode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Avsluta avsnittet med negativ belöning vid väggkollision.
        if (collision.gameObject.CompareTag("wall"))
        {
            AddReward(-1);
            Debug.Log("Robot collided with wall, ending episode!!!");
            EndEpisode();
        }

        if (collision.gameObject.name == "ChargeConnector")
        {
            AddReward(1);
            Debug.Log("Robot connected to charge connector, ending episode!!!");
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Gör det möjligt att styra roboten manuellt med tangentbordet.
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
        // Liten tidskostnad uppmuntrar agenten att hitta skräpet snabbt.
        AddReward(-0.001f);

        int movementAction =actions.DiscreteActions[0];
        int rotationAction = actions.DiscreteActions[1];

        if (movementAction == 1) // Move forward
        {
            // Flytta framåt med Rigidbody när sådan finns.
            Vector3 movement = transform.forward * moveSpeed * Time.fixedDeltaTime;
            if (body != null)
            {
                body.MovePosition(body.position + movement);
            }
            else
            {
                transform.position += movement;
            }
        }

        if (rotationAction == 1) // Rotate left  
        {
            // Rotera enligt agentens val.
            Rotate(-rotationSpeed * Time.fixedDeltaTime);
        }
        else if (rotationAction == 2)
        {
            Rotate(rotationSpeed * Time.fixedDeltaTime);
        }

    }

    private void Rotate(float degrees)
    {
        // Rotera fysikobjektet på ett stabilt sätt.
        Quaternion rotation = body != null
            ? body.rotation * Quaternion.Euler(0f, degrees, 0f)
            : transform.rotation * Quaternion.Euler(0f, degrees, 0f);

        if (body != null)
        {
            body.MoveRotation(rotation);
        }
        else
        {
            transform.rotation = rotation;
        }
    }

}
