using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

public class HummingBirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to rotate around the up axis")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at tip of the beak")]
    public Transform beakTip;

    [Tooltip("The agent's camera")]
    public Camera agentCamera;

    [Tooltip("Whether this is training mode or gameplay mode")]
    public bool trainingMode;

    //rigid body of the agent
    private Rigidbody rigidbody;

    //flower area that the agent is in
    private FlowerArea flowerArea;

    //Nearest flower to agent
    private Flower nearestFlower;

    //allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    //allows for smoother yaw changes
    private float smoothyawChanges = 0f;

    //max angle that the bird can pitch up or down
    private const float MaxPitchAngle = 80f;

    //max distance from the beak tip to accept nectar collision
    private const float BeakTipRadius = 0.008f;

    //whether the agent is frozen (intentionally not flying)
    private bool frozen = false;

    /// <summary>
    /// The amount of nectar the agent has obtained during the episode
    /// </summary>
    public float nectarObtained { get; private set; }

    /// <summary>
    /// initialize the agent
    /// </summary>
    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();

        if (!trainingMode) MaxStep = 0;
    }

    /// <summary>
    /// Reset the agent when episode begins
    /// </summary>
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            flowerArea.ResetFlowers();
        }
        
        nectarObtained = 0;

        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        bool inFrontofFlower = true;
        if (trainingMode)
        {
            inFrontofFlower = Random.value > 0.5;
        }

        MoveToSafeRandomPosition(inFrontofFlower);

        UpdateNearestFlower();
    }

    /// <summary>
    /// this is called when action is recieved from either the player input or the neural network
    /// 
    /// vectorAction[i] represents:
    /// 0 : move vector x (+1 = rifht, -1 = left)
    /// 1 : move vector y (+1 = up, -1 = down)
    /// 2 : move vector z (+1 = forward, -1 = backward)
    /// 3 : pitch angle (+1 = pitch up, -1 = pitch down)
    /// 4 : yaw angle (+1 = turn right, -1 = turn left)
    /// 
    /// </summary>
    /// <param name="vectorAction">Action to take</param>
    public override void OnActionReceived(float[] vectorAction)
    {
        if (frozen) return;

        Vector3 move = new Vector3(vectorAction[0], vectorAction[1], vectorAction[2]);

        rigidbody.AddForce(move * moveForce);

        Vector3 rotationVector = transform.rotation.eulerAngles;

        float pitchChange = vectorAction[3];
        float yawChange = vectorAction[4];

        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothyawChanges = Mathf.MoveTowards(smoothyawChanges, yawChange, 2f * Time.fixedDeltaTime);

        float pitch = rotationVector.x + smoothPitchChange * Time.fixedDeltaTime * pitchSpeed;
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -MaxPitchAngle, MaxPitchAngle);

        float yaw = rotationVector.y + smoothyawChanges * Time.fixedDeltaTime * yawSpeed;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Collect vector observation from the environment
    /// </summary>
    /// <param name="sensor">the vector sensor</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        sensor.AddObservation(transform.localRotation.normalized);

        Vector3 toFlower = nearestFlower.flowerCenterPos - beakTip.position;

        sensor.AddObservation(toFlower.normalized);

        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.flowerUpVector.normalized));

        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.flowerUpVector.normalized));

        sensor.AddObservation(toFlower.magnitude / FlowerArea.AreaDiameter);
    }

    /// <summary>
    /// When behavior type is set to heuristic on agent's behavior parameters 
    /// this function will be called. Its return value will be fed in 
    /// <see cref="OnActionReceived(float[])"/> instead of using the NN.
    /// </summary>
    /// <param name="actionsOut">Output Action Array</param>
    public override void Heuristic(float[] actionsOut)
    {
        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left= transform.right;

        if (Input.GetKey(KeyCode.UpArrow)) up = transform.up;
        else if (Input.GetKey(KeyCode.DownArrow)) up = -transform.up;

        if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;

        if (Input.GetKey(KeyCode.E)) pitch = 1f;
        else if (Input.GetKey(KeyCode.Q)) pitch = -1f;

        Vector3 combined = (forward + left + up).normalized;

        actionsOut[0] = combined.x;
        actionsOut[1] = combined.y;
        actionsOut[2] = combined.z;
        actionsOut[3] = pitch;
        actionsOut[4] = yaw;
    }

    public void FreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreze not supported in training mode");
        frozen = true;
        rigidbody.Sleep();
    }

    public void UnFreezeAgent()
    {
        Debug.Assert(trainingMode == false, "Freeze/Unfreze not supported in training mode");
        frozen = false;
        rigidbody.WakeUp();
    }

    /// <summary>
    /// Move the agen to a safe random position i.e. it won't collide with anything. If in front of flower
    /// then also point the beak towrds the flower
    /// </summary>
    /// <param name="inFrontofFlower">whether to choose a spot in front of a flower</param>
    private void MoveToSafeRandomPosition(bool inFrontofFlower)
    {
        bool safePositionFound = false;
        int attemptRemaining = 100;
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        while (attemptRemaining > 0 && !safePositionFound)
        {
            attemptRemaining--;
            if (inFrontofFlower)
            {
                Flower randomFlower = flowerArea.Flowers[Random.Range(0, flowerArea.Flowers.Count)];

                float distanceFromFLower = Random.Range(.1f, .2f);
                potentialPosition = randomFlower.transform.position + randomFlower.flowerUpVector * distanceFromFLower;

                Vector3 toFlower = randomFlower.flowerCenterPos - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
            }
            else
            {
                float height = Random.Range(1.2f, 2.5f);

                float radius = Random.Range(2f, 7f);

                Quaternion direction = Quaternion.Euler(0f, Random.Range(-180f, 180f), 0f);

                potentialPosition = flowerArea.transform.position + Vector3.up * height + (direction * Vector3.forward * radius);

                float pitch = Random.Range(-60f, 60f);
                float yaw = Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f);

            safePositionFound = colliders.Length == 0;
        }

        Debug.Assert(safePositionFound, "failed spawn!");

        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.Flowers)
        {
            if (nearestFlower == null && flower.hasNectar)
            {
                nearestFlower = flower;
            }
            else if (flower.hasNectar)
            {
                float disttoFlower = Vector3.Distance(flower.transform.position, beakTip.position);

                float distToCurrentnearestflower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                if (!nearestFlower.hasNectar || distToCurrentnearestflower > disttoFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    /// <summary>
    /// called when the agent's collider enters a trigger collider
    /// </summary>
    /// <param name="other">trigger colider</param>
    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterorStay(other);
    }

    /// <summary>
    /// Called when the agent's collider stays in a trigger collider
    /// </summary>
    /// <param name="other">the trigger collider</param>
    private void OnTriggerStay(Collider other)
    {
        TriggerEnterorStay(other);
    }

    /// <summary>
    /// Handles when agent's collider enters or stays in trigger collider
    /// </summary>
    /// <param name="collider">the trigger collider</param>
    private void TriggerEnterorStay(Collider collider)
    {
        if (collider.CompareTag("nectar"))
        {
            Vector3 closestPointtoBeakTip = collider.ClosestPoint(beakTip.position);

            if (Vector3.Distance(beakTip.position,closestPointtoBeakTip)<BeakTipRadius)
            {
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                float nectarRecieved = flower.Feed(.01f);

                nectarObtained += nectarRecieved;

                if (trainingMode)
                {
                    float bonus = 0.02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.flowerUpVector.normalized));
                    AddReward(0.01f + bonus);
                }

                if (!flower.hasNectar)
                {
                    UpdateNearestFlower();
                }
            }
        }
    }

    /// <summary>
    /// called when the agent collides with something
    /// </summary>
    /// <param name="collision"></param>
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary"))
        {
            AddReward(-0.5f);
        }
    }

    private void Update()
    {
        if(nearestFlower!=null)
        {
            Debug.DrawLine(beakTip.position, nearestFlower.flowerCenterPos, Color.green);
        }
    }

    private void FixedUpdate()
    {
        if (nearestFlower != null && !nearestFlower.hasNectar)
            UpdateNearestFlower();
    }
}
