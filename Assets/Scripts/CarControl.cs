using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class CarControl : Agent
{
    public float enginePower = 2000.0f; // Engine power
    public float turnSpeed = 25.0f; // Maximum turn speed
    public float turnSmoothness = 5.0f; // Turn smoothness
    public Transform[] wheels; // Array of Wheel Collider components
    public Transform[] wheelMeshes; // Array of wheel meshes
    public Transform centerOfMass; // Center of Mass point
    public GameObject steeringWheel;
    public Transform meetingPoint;
    public Terrain terrain;
    [Min(0.0f)]
    public float carSurfaceOffset = 0.05f;

    public float meetingPointDistance = 2.0f;
    public float maximumPointTime = 10.0f;
    public float stationarySpeedThreshold = 0.5f;
    public float stationaryCompletionReward = 5.0f;

    private Rigidbody rb; // Car's Rigidbody component
    private float currentTurnAngle = 0.0f;
    private bool touchingPoint;
    private float pointContactTime;

    float horizontalInput = 0.0f;
    float throttle = 0.0f;


    public  float PermissibleDistanceToMeetingPoint = 2.0f;

    Vector3 OriginalPosition;
    Quaternion OriginalRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = centerOfMass.localPosition; // Set center of mass
        OriginalPosition = transform.position;
        OriginalRotation = transform.rotation;
    }


    public override void OnEpisodeBegin()
    {
        // Reset car position and rotation
        transform.position = OriginalPosition;
        transform.rotation = OriginalRotation;

        // Reset velocity
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (terrain != null)
        {
            RandomTerrainGenerator terrainGenerator =
                terrain.GetComponent<RandomTerrainGenerator>();

            if (terrainGenerator != null)
            {
                //terrainGenerator.GenerateRandomTerrain();
            }
        }

        PlaceCarAboveTerrain();

        if (meetingPoint != null)
        {
            PointTerrainPlacement pointPlacement =
                meetingPoint.GetComponent<PointTerrainPlacement>();

            if (pointPlacement != null)
            {
                pointPlacement.PlaceAboveTerrain();
            }
        }

        // Reset inputs
        horizontalInput = 0.0f;
        throttle = 0.0f;
        touchingPoint = false;
        pointContactTime = 0.0f;
    }

    private void PlaceCarAboveTerrain()
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        Collider[] carColliders = GetComponentsInChildren<Collider>();
        if (carColliders.Length == 0)
        {
            return;
        }

        float lowestColliderPoint = float.PositiveInfinity;
        foreach (Collider carCollider in carColliders)
        {
            lowestColliderPoint = Mathf.Min(
                lowestColliderPoint,
                carCollider.bounds.min.y);
        }

        float terrainHeight = terrain.SampleHeight(transform.position);
        float targetLowestPoint = terrainHeight + carSurfaceOffset;
        transform.position += Vector3.up * (targetLowestPoint - lowestColliderPoint);
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        AddReward(-0.00001f * DistanceTotMeetingPoint()); // Small penalty for each step to encourage faster completion


        if(transform.position.y < -1.0f)
        {
            AddReward(-1.0f); // Punishment for falling off the track
            Debug.Log("MIssion failed: " + GetCumulativeReward());
            EndEpisode();
        }

        horizontalInput = actions.ContinuousActions[0];
        throttle = actions.ContinuousActions[1];
    }

    public float DistanceTotMeetingPoint()
    {
        Vector3 MeetingPointXZ = new Vector3(meetingPoint.position.x, 0, meetingPoint.position.z);
        Vector3 CarXZ = new Vector3(transform.position.x, 0, transform.position.z);

        //Returnerar avståndet i ZX-koordinaterna mellan bilen och mötespunkten. Detta används för att ge en belöning eller straff baserat på hur nära bilen är mötespunkten.
        return Vector3.Distance(CarXZ, MeetingPointXZ);
    }

    public float CurrentSpeed()
    {
        return rb.linearVelocity.magnitude;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var Actions = actionsOut.ContinuousActions;


        if (Input.GetKey(KeyCode.LeftArrow))
        {
            Actions[0] = -1.0f;
        }
        
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            Actions[0] = 1.0f;
        }

        
        if (Input.GetKey(KeyCode.UpArrow))
        {
            Actions[1] = 1.0f;
        }
        
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            Actions[1] = -1.0f;
        }
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Meetingpointens lokala X/Z-position relativt bilen.
        // Y tas bort eftersom meetingpointens höjd bestäms av terrängen.
        Vector3 relativeGoalWorld = meetingPoint.position - transform.position;
        relativeGoalWorld.y = 0.0f;

        // Omvandlar målvektorn från världens koordinater till bilens lokala koordinater.
        Vector3 localGoalXZ =
            transform.InverseTransformDirection(relativeGoalWorld);

        // Skickar endast X och Z för meetingpointen, inte Y.
        sensor.AddObservation(localGoalXZ.x);
        sensor.AddObservation(localGoalXZ.z);

        // Bilens lokala linjära hastighet: framåt/bakåt, sidled och vertikalt.
        Vector3 relativeVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(relativeVelocity);

        // Bilens lokala upp-vektor visar hur mycket bilen lutar i terrängen.
        sensor.AddObservation(transform.InverseTransformDirection(Vector3.up));
    }


    void FixedUpdate()
    {

        //Debug.Log("Distance to meeting point: " + DistanceTotMeetingPoint());

        if (touchingPoint)
        {
            pointContactTime += Time.fixedDeltaTime;

            if (pointContactTime >= maximumPointTime)
            {
                float stationaryFactor = Mathf.Clamp01(
                    1.0f - CurrentSpeed() / stationarySpeedThreshold);
                AddReward(maximumPointTime + stationaryCompletionReward * stationaryFactor);
                Debug.Log("MIssion completed: " + GetCumulativeReward());
                touchingPoint = false;
                pointContactTime = 0.0f;
                EndEpisode();
            }
        }
                  
        // Calculate turn angle
        float targetTurnAngle = horizontalInput * turnSpeed;
        currentTurnAngle = Mathf.Lerp(currentTurnAngle, targetTurnAngle, Time.deltaTime * turnSmoothness);

        steeringWheel.transform.localEulerAngles = new Vector3(-64, 0, currentTurnAngle * 3);


        // Wheels rotation and steering
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheelCollider = wheels[i].GetComponent<WheelCollider>();
            if (i < 2) // First two wheels steer
            {
                wheelCollider.steerAngle = currentTurnAngle;
            }
            else
            {
                wheelCollider.steerAngle = 0f; // Other two wheels remain straight
            }

            // Wheels driving (forward/backward movement)
            wheelCollider.motorTorque = throttle * enginePower;

            // Rotate wheel meshes
            Quaternion wheelRotation;
            Vector3 wheelPosition;
            wheelCollider.GetWorldPose(out wheelPosition, out wheelRotation);
            wheelMeshes[i].position = wheelPosition;
            wheelMeshes[i].rotation = wheelRotation;
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.name == "MeetingPoint")
        {
            if (!touchingPoint)
            {
                touchingPoint = true;
                pointContactTime = 0.0f;
            }
        }
        }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == "MeetingPoint")
        {
            if (touchingPoint)
            {
                AddReward(Mathf.Min(pointContactTime, maximumPointTime));
                Debug.Log("MIssion failed: " + GetCumulativeReward());
                touchingPoint = false;
                pointContactTime = 0.0f;
                EndEpisode();
            }
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if(other.gameObject.name == "Terrain")
        {
            AddReward(-1.0f); // Punishment for leaving the area
            Debug.Log("MIssion failed: " + GetCumulativeReward());
            EndEpisode();
        }
    }

}
