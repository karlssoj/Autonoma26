using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;


public class CleanerController : Agent
{
    // Inställningar för robotens rörelse.
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float distanceRewardScale = 0.01f;


    public float MaxForce = 0;
    public float MaxSpeed = 0;
 
    // Referenser till robotens startläge och miljö.
    private Vector3 startingPosition;
    public GameObject Trash;
    private Quaternion startingRotation;
    private Rigidbody body;
    private float previousGoalDistance;
    public GameObject Nose;
    public GameObject CargePoint;

    private void Awake()
    {
        // Spara startläget och hitta fysikkomponenten.
        startingPosition = transform.position;
        startingRotation = transform.rotation;
        body = GetComponent<Rigidbody>();
    }


    public override void OnEpisodeBegin()
    {
        // Återställ miljön när ett nytt träningsavsnitt börjar.
        Reset();
        previousGoalDistance = GoalDistance();
    }

    public void Reset()
    {
        // Flytta tillbaka roboten och skapa en ny vägglayout.
        transform.SetPositionAndRotation(startingPosition, startingRotation);

        foreach (MidWallRandomizer midWall in transform.parent.GetComponentsInChildren<MidWallRandomizer>())
        {
            midWall.RandomizePosition();
        }
    
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

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
    }

    public void Docked()
    {
        // Belöna agenten när den ansluter till laddningskontakten.
        AddReward(1);
        Debug.Log("Robot connected to charge connector, ending episode!!!");
        EndEpisode();
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Först räknas en pil från robotens nos till dockningspunkten ut.
        // InverseTransformDirection omvandlar sedan pilen från världens koordinater
        // till robotens egna koordinater. Positiv x betyder då alltid "åt höger"
        // och positiv z betyder "framåt" sett från roboten.
        // På så sätt behöver agenten inte veta åt vilket håll roboten pekar i världen.
        // Samma situation ger samma observation även om roboten har vänt sig.
        Vector3 relativeGoal = transform.InverseTransformDirection(CargePoint.transform.position - Nose.transform.position);
        sensor.AddObservation(relativeGoal);

        // Hastigheten omvandlas också till robotens koordinater. Agenten kan därför
        // skilja på att köra framåt, bakåt eller åt sidan utan att känna till sin
        // absoluta rotation i världen.
        sensor.AddObservation(transform.InverseTransformDirection(body.linearVelocity));
    }


    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Gör det möjligt att styra roboten manuellt med tangentbordet.
        var discreteActionsOut = actionsOut.DiscreteActions;
        var continuousActionsOut = actionsOut.ContinuousActions;

        continuousActionsOut[0] = 0; // Movement action
        discreteActionsOut[0] = 0; // Rotation action

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.upArrowKey.isPressed)
        {
            continuousActionsOut[0] = 1.0f; // Move forward
        }

        else if (Keyboard.current.downArrowKey.isPressed)
        {
            continuousActionsOut[0] = -1.0f; // Move backward
        }

        if (Keyboard.current.leftArrowKey.isPressed)
        {
            discreteActionsOut[0] = 1; // Rotate left
        }
        else if (Keyboard.current.rightArrowKey.isPressed)
        {
            discreteActionsOut[0] = 2; // Rotate right
        }
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        // Varje beslut kostar belöning: stillastående ger -0.001, maximal hastighet ger -0.0001,
        // och hastigheter däremellan får en linjärt interpolerad kostnad. Clamp håller värdet 0-1.
        float normalizedSpeed = MaxSpeed > 0f
            ? Mathf.Clamp01(body.linearVelocity.magnitude / MaxSpeed)
            : 0f;
        float movementCost = Mathf.Lerp(-0.001f, -0.0001f, normalizedSpeed);
        AddReward(movementCost);

        float movementAction = actions.ContinuousActions[0];
        int rotationAction = actions.DiscreteActions[0];


        if(body.linearVelocity.magnitude < MaxSpeed)
        {
            body.AddRelativeForce(0, 0, movementAction * MaxForce);
        }

        Vector3 forwardVelocity = Vector3.Project(body.linearVelocity, transform.forward);
        body.linearVelocity = forwardVelocity;

        if (rotationAction == 1) // Rotate left  
        {
            // Rotera enligt agentens val.
            Rotate(-rotationSpeed * Time.fixedDeltaTime);
        }
        else if (rotationAction == 2) // Rotate right
        {
            Rotate(rotationSpeed * Time.fixedDeltaTime);
        }

        float currentGoalDistance = GoalDistance();
        AddReward((previousGoalDistance - currentGoalDistance) * distanceRewardScale);
        previousGoalDistance = currentGoalDistance;

    }

    private float GoalDistance()
    {
        return Vector3.Distance(Nose.transform.position, CargePoint.transform.position);
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
