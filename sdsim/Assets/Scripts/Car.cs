using UnityEngine;
using System.Collections;


public class Car : MonoBehaviour, ICar{

    public CarSpawner carSpawner;
    public WheelCollider[] wheelColliders;
    public Transform[] wheelMeshes;

    public float maxSpeed = 30f;
    public float maxTorque = 50f;
    public float maxBreakTorque = 50f;
    public AnimationCurve torqueCurve;

    public Transform centrOfMass;

    public float requestTorque = 0f;
    public float requestBrake = 0f;
    public float requestSteering = 0f;

    public Vector3 acceleration = Vector3.zero;
    public Vector3 velocity = Vector3.zero;
    public Vector3 prevVel = Vector3.zero;

    public Vector3 startPos;
    public Quaternion startRot;
    private Quaternion rotation = Quaternion.identity;
    private Vector3 gyro = Vector3.zero;

    public Rigidbody rb;

    //for logging
    public float lastSteer = 0.0f;
    public float lastAccel = 0.0f;

    //when the car is doing multiple things, we sometimes want to sort out parts of the training
    //use this label to pull partial training samples from a run 
    public string activity = "keep_lane";

    public float maxSteer = 16.0f;

    //name of the last object we hit.
    public string last_collision = "none";

    // ---------- DIFFERENTIAL DRIVE ADDITIONS ----------
    [Header("Differential Drive Settings")]
    public bool useDifferentialDrive = false;   // set true in Inspector for diff-drive cars

    // Parameters from config.py (adjustable in Inspector)
    public float diffMaxSpeed = 150f;           // MAX_SPEED
    public float diffWheelBase = 30f;            // WHEEL_BASE
    public float diffTurnGain = 1.2f;            // TURN_GAIN
    public float diffMotorResponse = 12f;        // MOTOR_RESPONSE
    public float diffTurnResponse = 7f;          // TURN_RESPONSE
    public float diffLinearDrag = 0.6f;          // LINEAR_DRAG
    public float diffAngularDrag = 1.0f;         // ANGULAR_DRAG

    // Differential drive state
    private float targetLeft, targetRight;       // target motor speeds (normalised -1..1)
    private float leftSpeed, rightSpeed;          // current smoothed speeds
    private float omega;                           // current angular velocity (rad/s)
    private Vector2 groundPos;                      // position on XZ plane (x = x, y = z)
    private float theta;                            // heading in radians (0 = +X)
    // -------------------------------------------------

    // Use this for initialization
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb && centrOfMass)
        {
            rb.centerOfMass = centrOfMass.localPosition;
        }

        requestTorque = 0f;
        requestSteering = 0f;

        SavePosRot();

        // had to disable this because PID max steering was affecting the global max_steering
        // maxSteer = PlayerPrefs.GetFloat("max_steer", 16.0f);
    }

    void Start()
    {
        if (useDifferentialDrive)
        {
            // Disable wheel colliders (they would interfere with kinematic motion)
            foreach (var wc in wheelColliders)
            {
                wc.enabled = false;
            }
            // Set rigidbody kinematic (we will update transform directly)
            if (rb) rb.isKinematic = true;

            // Initialise diff-drive state from current transform
            groundPos = new Vector2(transform.position.z, transform.position.x);
            theta = transform.eulerAngles.y * Mathf.Deg2Rad;   // yaw, 0 = facing +Z
        }
    }

    public void SavePosRot()
    {
        startPos = transform.position;
        startRot = transform.rotation;
    }

    public void RestorePosRot()
    {
        Set(startPos, startRot);

        if (useDifferentialDrive)
        {
            // After resetting transform, reinitialise diff-drive state
            groundPos = new Vector2(startPos.z, startPos.x);
            theta = startRot.eulerAngles.y * Mathf.Deg2Rad;
            leftSpeed = rightSpeed = targetLeft = targetRight = 0f;
            omega = 0f;
        }
    }

    public void RequestThrottle(float val)
    {
        requestTorque = val;
        requestBrake = 0f;
        //Debug.Log("request throttle: " + val);
    }

    public void SetMaxSteering(float val)
    {
        maxSteer = val;
        // had to disable this because PID max steering was affecting the global max_steering
        // PlayerPrefs.SetFloat("max_steer", maxSteer);
        // PlayerPrefs.Save();
    }

    public float GetMaxSteering()
    {
        return maxSteer;
    }

    public void RequestSteering(float val)
    {
        requestSteering = Mathf.Clamp(val, -maxSteer, maxSteer);
        //Debug.Log("request steering: " + val);
    }

    // ---------- NEW METHOD FOR DIFFERENTIAL DRIVE ----------
    public void SetMotorSpeeds(float left, float right)
    {
        if (!useDifferentialDrive) return;
        targetLeft = Mathf.Clamp(left, -1f, 1f);
        targetRight = Mathf.Clamp(right, -1f, 1f);
    }

    public void Set(Vector3 pos, Quaternion rot)
    {
        rb.position = pos;
        rb.rotation = rot;

        //just setting it once doesn't seem to work. Try setting it multiple times..
        StartCoroutine(KeepSetting(pos, rot, 1));
    }

    IEnumerator KeepSetting(Vector3 pos, Quaternion rot, int numIter)
    {
        while (numIter > 0)
        {
            rb.isKinematic = true;

            yield return new WaitForFixedUpdate();

            rb.position = pos;
            rb.rotation = rot;
            transform.position = pos;
            transform.rotation = rot;

            numIter--;

            rb.isKinematic = false;
        }
    }

    public float GetSteering()
    {
        return requestSteering;
    }

    public float GetThrottle()
    {
        return requestTorque;
    }

    public float GetFootBrake()
    {
        return requestBrake;
    }

    public float GetHandBrake()
    {
        return 0.0f;
    }

    public Vector3 GetVelocity()
    {
        return velocity;
    }

    public Vector3 GetAccel()
    {
        return acceleration;
    }
    public Vector3 GetGyro()
    {
        return gyro;
    }
    public float GetOrient()
    {
        Vector3 dir = transform.forward;
        return Mathf.Atan2(dir.z, dir.x);
    }

    public Transform GetTransform()
    {
        return this.transform;
    }

    public bool IsStill()
    {
        return rb.IsSleeping();
    }

    public void RequestFootBrake(float val)
    {
        requestBrake = val;
    }

    public void RequestHandBrake(float val)
    {
        //todo
    }

    // Update is called once per frame
    void Update()
    {
        UpdateWheelPositions();
    }

    public string GetActivity()
    {
        return activity;
    }

    public void SetActivity(string act)
    {
        activity = act;
    }

    void FixedUpdate()
    {
        if (useDifferentialDrive)
        {
            float dt = Time.fixedDeltaTime;

            // Motor smoothing
            float alphaMotor = 1f - Mathf.Exp(-diffMotorResponse * dt);
            leftSpeed += (targetLeft - leftSpeed) * alphaMotor;
            rightSpeed += (targetRight - rightSpeed) * alphaMotor;

            float vL = leftSpeed * diffMaxSpeed;
            float vR = rightSpeed * diffMaxSpeed;

            float v = (vL + vR) / 2f;
            float omegaTarget = (vR - vL) / diffWheelBase * diffTurnGain;

            // Angular inertia
            float alphaTurn = 1f - Mathf.Exp(-diffTurnResponse * dt);
            omega += (omegaTarget - omega) * alphaTurn;

            // Drag
            v *= (1f - diffLinearDrag * dt);
            omega *= (1f - diffAngularDrag * dt);

            // Pose integration (ICC)
            if (Mathf.Abs(omega) < 1e-6f)
            {
                groundPos.x += v * Mathf.Cos(theta) * dt;   // move in +Z
                groundPos.y += v * Mathf.Sin(theta) * dt;   // move in +X
            }
            else
            {
                float R = v / omega;
                float iccZ = groundPos.x - R * Mathf.Sin(theta);   // icc in Z
                float iccX = groundPos.y + R * Mathf.Cos(theta);   // icc in X

                float dTheta = omega * dt;
                float cosD = Mathf.Cos(dTheta);
                float sinD = Mathf.Sin(dTheta);

                float dz = groundPos.x - iccZ;
                float dx = groundPos.y - iccX;

                groundPos.x = cosD * dz - sinD * dx + iccZ;
                groundPos.y = sinD * dz + cosD * dx + iccX;
                theta += dTheta;
            }

            // Apply to Unity Transform (X from groundPos.y, Z from groundPos.x)
            transform.position = new Vector3(groundPos.y, transform.position.y, groundPos.x);
            transform.rotation = Quaternion.Euler(0f, theta * Mathf.Rad2Deg, 0f);

            // Update velocity/accel for telemetry
            Vector3 newVel = (transform.position - new Vector3(prevVel.x, 0, prevVel.z)) / dt;
            acceleration = (newVel - velocity) / dt;
            velocity = newVel;
            prevVel = transform.position;
            gyro = new Vector3(0, omega, 0);
        }
        else
        {
            // ---------- ORIGINAL ACKERMANN / WHEEL COLLIDER CODE ----------
            lastSteer = requestSteering;
            lastAccel = requestTorque;
            prevVel = velocity;
            velocity = transform.InverseTransformDirection(rb.velocity);
            acceleration = (velocity - prevVel) / Time.deltaTime;
            gyro = rb.angularVelocity;
            rotation = rb.rotation;

            // use the torque curve
            float throttle = torqueCurve.Evaluate(velocity.magnitude / maxSpeed) * requestTorque * maxTorque;
            float steerAngle = requestSteering;
            float brake = requestBrake * maxBreakTorque;

            //front two tires.
            wheelColliders[2].steerAngle = steerAngle;
            wheelColliders[3].steerAngle = steerAngle;

            //four wheel drive at the moment
            foreach (WheelCollider wc in wheelColliders)
            {
                wc.motorTorque = throttle;
                wc.brakeTorque = brake;
            }
        }
    }

    void FlipUpright()
    {
        Quaternion rot = Quaternion.Euler(180f, 0f, 0f);
        this.transform.rotation = transform.rotation * rot;
        transform.position = transform.position + Vector3.up * 2;
    }

    void UpdateWheelPositions()
    {
        // If wheel colliders are disabled, GetWorldPose may not work, but we still call it.
        // In diff-drive mode, wheels are disabled, so we skip or just leave meshes as they are.
        if (!useDifferentialDrive)
        {
            Quaternion rot;
            Vector3 pos;

            for (int i = 0; i < wheelColliders.Length; i++)
            {
                WheelCollider wc = wheelColliders[i];
                Transform tm = wheelMeshes[i];

                wc.GetWorldPose(out pos, out rot);

                tm.position = pos;
                tm.rotation = rot;
            }
        }
        // If diff-drive, we could optionally animate wheels based on speed, but not required.
    }

    //get the name of the last object we collided with
    public string GetLastCollision()
    {
        return last_collision;
    }

    public void ClearLastCollision()
    {
        last_collision = "none";
    }

    void OnCollisionEnter(Collision col)
    {
        last_collision = col.gameObject.name;
    }
}