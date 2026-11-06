using UnityEngine;
using UnityEngine.InputSystem;

public enum OffRoadVehicleControl
{
    Accelerate,
    Brake,
    SteerLeft,
    SteerRight,
    Handbrake
}

[RequireComponent(typeof(Rigidbody))]
public sealed class OffRoadVehicleController : MonoBehaviour
{
    [Header("Drivetrain")]
    [SerializeField] private float maxMotorTorque = 1200f;
    [SerializeField] private float maxBrakeTorque = 3600f;
    [SerializeField] private float maxSteerAngle = 31f;
    [SerializeField] private float accelerationAssist = 3.2f;
    [SerializeField] private float uprightAssist = 5f;
    [SerializeField] private float steeringAssist = 0.65f;
    [SerializeField] private float maxSpeedKmh = 130f;

    private readonly WheelCollider[] wheels = new WheelCollider[4];
    private readonly Transform[] wheelVisuals = new Transform[4];
    private readonly bool[] heldControls = new bool[5];
    private ParticleSystem[] dust;
    private Rigidbody body;
    private AudioSource engineAudio;
    private AudioSource roadAudio;
    private AudioSource skidAudio;
    private AudioSource impactAudio;
    private Material bodyMaterial;
    private Material tireMaterial;
    private Transform chassisVisual;
    private Transform cabinVisual;
    private float throttle;
    private float steer;
    private float brake;
    private bool handbrake;
    private bool aiControlled;
    private bool controlsLocked;
    private bool buggyStyle;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float damage;
    private float overturnedTime;
    private float lastImpactTime = -1f;

    public float SpeedKmh => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
    public float EngineRpm { get; private set; } = 850f;
    public int CurrentGear { get; private set; } = 1;
    public bool IsSkidding { get; private set; }
    public bool IsAIControlled => aiControlled;
    public float NormalizedSpeed => Mathf.Clamp01(SpeedKmh / 165f);

    public static OffRoadVehicleController Create(string objectName, Vector3 position, Vector3 nextPoint,
        Color paint, bool ai)
    {
        GameObject vehicle = new GameObject(objectName);
        vehicle.tag = ai ? "Untagged" : "Player";
        vehicle.transform.position = position + Vector3.up * 0.08f;
        Vector3 direction = nextPoint - position;
        direction.y = 0f;
        vehicle.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        OffRoadVehicleController controller = vehicle.AddComponent<OffRoadVehicleController>();
        controller.BuildVehicle(paint, ai);
        return controller;
    }

    private void BuildVehicle(Color paint, bool ai)
    {
        aiControlled = ai;
        body = GetComponent<Rigidbody>();
        body.mass = 1580f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.centerOfMass = new Vector3(0f, -0.48f, 0.02f);
        body.angularDamping = 2f;
        body.linearDamping = 0.04f;

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        bodyMaterial = CreateMaterial("Enamel // " + name, paint, 0.28f);
        Material dark = CreateMaterial("Graphite trim", new Color(0.075f, 0.09f, 0.095f), 0.15f);
        Material glass = CreateMaterial("Smoked glass", new Color(0.045f, 0.095f, 0.12f), 0.7f);
        Material rubber = CreateMaterial("All-terrain rubber", new Color(0.065f, 0.07f, 0.065f), 0f);
        Material metal = CreateMaterial("Brushed steel", new Color(0.47f, 0.49f, 0.47f), 0.75f);
        tireMaterial = rubber;
        float chassisLength = buggyStyle ? 3.1f : 3.8f;
        float bodyY = buggyStyle ? 0.98f : 1.08f;

        BoxCollider chassis = gameObject.AddComponent<BoxCollider>();
        chassis.center = new Vector3(0f, bodyY, 0f);
        chassis.size = new Vector3(1.85f, 0.72f, chassisLength);

        chassisVisual = CreatePart(PrimitiveType.Cube, "Armoured chassis", new Vector3(0f, bodyY, 0f),
            new Vector3(1.9f, 0.67f, chassisLength), bodyMaterial, false).transform;
        float cabinLength = buggyStyle ? 1.2f : 1.65f;
        cabinVisual = CreatePart(PrimitiveType.Cube, "Raised cabin", new Vector3(0f, bodyY + 0.62f, -0.34f),
            new Vector3(1.46f, 0.78f, cabinLength), bodyMaterial, false).transform;
        float cabinFront = -0.34f + cabinLength * 0.5f;
        float cabinRear = -0.34f - cabinLength * 0.5f;
        CreatePart(PrimitiveType.Cube, "Front windshield", new Vector3(0f, bodyY + 0.68f, cabinFront + 0.055f),
            new Vector3(1.28f, 0.52f, 0.055f), glass, false).transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        CreatePart(PrimitiveType.Cube, "Rear window", new Vector3(0f, bodyY + 0.68f, cabinRear - 0.035f),
            new Vector3(1.2f, 0.46f, 0.055f), glass, false).transform.localRotation = Quaternion.Euler(16f, 0f, 0f);

        for (int side = -1; side <= 1; side += 2)
        {
            CreatePart(PrimitiveType.Cube, "Front door window", new Vector3(side * 0.747f, bodyY + 0.68f, 0.10f),
                new Vector3(0.055f, 0.48f, 0.58f), glass, false);
            CreatePart(PrimitiveType.Cube, "Rear door window", new Vector3(side * 0.747f, bodyY + 0.68f, -0.72f),
                new Vector3(0.055f, 0.46f, 0.52f), glass, false);
            CreatePart(PrimitiveType.Cube, "Window pillar", new Vector3(side * 0.755f, bodyY + 0.68f, -0.30f),
                new Vector3(0.075f, 0.53f, 0.08f), dark, false);
            CreatePart(PrimitiveType.Cube, "Front door handle", new Vector3(side * 0.782f, bodyY + 0.29f, -0.02f),
                new Vector3(0.035f, 0.055f, 0.20f), metal, false);
            CreatePart(PrimitiveType.Cube, "Rear door handle", new Vector3(side * 0.782f, bodyY + 0.29f, -0.59f),
                new Vector3(0.035f, 0.055f, 0.18f), metal, false);
            CreatePart(PrimitiveType.Cube, "Rock slider", new Vector3(side * 0.99f, bodyY - 0.28f, 0f),
                new Vector3(0.16f, 0.16f, 2.25f), dark, false);
            CreatePart(PrimitiveType.Cube, "Front fender flare", new Vector3(side * 0.99f, 0.97f, 1.25f),
                new Vector3(0.18f, 0.11f, 0.94f), dark, false);
            CreatePart(PrimitiveType.Cube, "Rear fender flare", new Vector3(side * 0.99f, 0.97f, -1.25f),
                new Vector3(0.18f, 0.11f, 0.94f), dark, false);
            CreatePart(PrimitiveType.Cube, "Side mirror", new Vector3(side * 1.02f, bodyY + 0.62f, 0.48f),
                new Vector3(0.2f, 0.15f, 0.22f), dark, false);
        }

        CreatePart(PrimitiveType.Cube, "Front bull bar", new Vector3(0f, 0.63f, chassisLength * 0.5f + 0.16f),
            new Vector3(2.1f, 0.18f, 0.22f), metal, false);
        CreatePart(PrimitiveType.Cube, "Rear recovery bumper", new Vector3(0f, 0.63f, -chassisLength * 0.5f - 0.14f),
            new Vector3(2.05f, 0.18f, 0.2f), dark, false);
        Material lamp = CreateMaterial("LED headlamp", new Color(0.93f, 0.82f, 0.57f), 0f);
        Material tailLamp = CreateMaterial("Tail lamps", new Color(0.78f, 0.055f, 0.025f), 0f);
        Material grille = CreateMaterial("Front grille", new Color(0.045f, 0.055f, 0.055f), 0.15f);
        float front = chassisLength * 0.5f;
        float rear = -chassisLength * 0.5f;
        CreatePart(PrimitiveType.Cube, "Sculpted hood", new Vector3(0f, bodyY + 0.35f, 1.29f),
            new Vector3(1.63f, 0.25f, 1.16f), bodyMaterial, false);
        CreatePart(PrimitiveType.Cube, "Radiator grille", new Vector3(0f, 0.91f, front + 0.04f),
            new Vector3(0.78f, 0.44f, 0.08f), grille, false);
        for (int i = -3; i <= 3; i++)
        {
            CreatePart(PrimitiveType.Cube, "Grille slot", new Vector3(i * 0.1f, 0.91f, front + 0.09f),
                new Vector3(0.035f, 0.34f, 0.025f), metal, false);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            CreatePart(PrimitiveType.Sphere, "Headlamp", new Vector3(side * 0.68f, 1.0f, chassisLength * 0.5f + 0.08f),
                new Vector3(0.25f, 0.19f, 0.1f), lamp, false);
            CreatePart(PrimitiveType.Cube, "Rear LED tail lamp", new Vector3(side * 0.72f, 0.9f, rear - 0.04f),
                new Vector3(0.25f, 0.38f, 0.09f), tailLamp, false);
        }

        CreateSpareWheel(dark, metal);
        CreateWheels(rubber, metal);
        CreateDustEmitters();
        ConfigureEngineAudio();
    }

    private void CreateWheels(Material rubber, Material metal)
    {
        Vector3[] positions =
        {
            new Vector3(-0.98f, 0.56f, 1.25f),
            new Vector3(0.98f, 0.56f, 1.25f),
            new Vector3(-0.98f, 0.56f, -1.25f),
            new Vector3(0.98f, 0.56f, -1.25f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject wheelObject = new GameObject("WheelCollider " + i);
            wheelObject.transform.SetParent(transform, false);
            wheelObject.transform.localPosition = positions[i];
            WheelCollider wheel = wheelObject.AddComponent<WheelCollider>();
            wheel.radius = 0.47f;
            wheel.suspensionDistance = 0.34f;
            wheel.mass = 32f;
            wheel.wheelDampingRate = 1.1f;
            JointSpring suspension = wheel.suspensionSpring;
            suspension.spring = 42000f;
            suspension.damper = 5600f;
            suspension.targetPosition = 0.52f;
            wheel.suspensionSpring = suspension;
            wheel.forceAppPointDistance = 0.12f;
            SetFriction(wheel, 1.55f, 1.32f);
            wheels[i] = wheel;

            GameObject visualRoot = new GameObject("Wheel visual");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = positions[i];
            wheelVisuals[i] = visualRoot.transform;

            GameObject tire = CreatePart(PrimitiveType.Cylinder, "Mud-terrain tire", positions[i],
                new Vector3(0.94f, 0.2f, 0.94f), rubber, false);
            tire.transform.SetParent(visualRoot.transform, false);
            tire.transform.localPosition = Vector3.zero;
            tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            GameObject hub = CreatePart(PrimitiveType.Cylinder, "Forged wheel hub",
                positions[i], new Vector3(0.56f, 0.055f, 0.56f), metal, false);
            hub.transform.SetParent(visualRoot.transform, false);
            hub.transform.localPosition = new Vector3(positions[i].x < 0f ? -0.17f : 0.17f, 0f, 0f);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            for (int treadIndex = 0; treadIndex < 16; treadIndex++)
            {
                float angle = treadIndex * 22.5f * Mathf.Deg2Rad;
                GameObject tread = CreatePart(PrimitiveType.Cube, "Tire tread block", positions[i],
                    new Vector3(0.13f, 0.12f, 0.12f), rubber, false);
                tread.transform.SetParent(visualRoot.transform, false);
                tread.transform.localPosition = new Vector3(0f, Mathf.Sin(angle) * 0.455f, Mathf.Cos(angle) * 0.455f);
                tread.transform.localRotation = Quaternion.Euler(0f, -treadIndex * 22.5f, 0f);
            }
        }
    }

    private void CreateSpareWheel(Material dark, Material metal)
    {
        GameObject spare = CreatePart(PrimitiveType.Cylinder, "Rear mounted spare tire",
            new Vector3(0f, 1.13f, -2.13f), new Vector3(0.88f, 0.23f, 0.88f), tireMaterial, false);
        spare.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        GameObject hub = CreatePart(PrimitiveType.Cylinder, "Spare wheel rim",
            new Vector3(0f, 1.13f, -2.28f), new Vector3(0.48f, 0.10f, 0.48f), metal, false);
        hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        CreatePart(PrimitiveType.Cube, "Spare tire mount",
            new Vector3(0f, 1.13f, -2.19f), new Vector3(0.2f, 0.2f, 0.12f), dark, false);
    }

    private void CreateDustEmitters()
    {
        dust = new ParticleSystem[4];
        for (int i = 0; i < wheels.Length; i++)
        {
            GameObject emitter = new GameObject("Wheel dust");
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = wheels[i].transform.localPosition;
            ParticleSystem particles = emitter.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
            main.startColor = new Color(0.56f, 0.43f, 0.29f, 0.62f);
            main.maxParticles = 80;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.28f;
            particles.transform.localRotation = Quaternion.Euler(115f, 0f, 0f);
            dust[i] = particles;
        }
    }

    private void ConfigureEngineAudio()
    {
        engineAudio = gameObject.AddComponent<AudioSource>();
        engineAudio.clip = CreateEngineClip();
        engineAudio.loop = true;
        engineAudio.volume = 0.28f;
        engineAudio.spatialBlend = 0.72f;
        engineAudio.minDistance = 3f;
        engineAudio.maxDistance = 70f;
        engineAudio.Play();

        if (!aiControlled && !controlsLocked)
        {
            roadAudio = gameObject.AddComponent<AudioSource>();
            roadAudio.clip = OffRoadAudioManager.RoadClip;
            roadAudio.loop = true;
            roadAudio.playOnAwake = false;
            roadAudio.volume = 0f;
            roadAudio.spatialBlend = 0f;
            roadAudio.Play();
        }

        skidAudio = gameObject.AddComponent<AudioSource>();
        skidAudio.clip = OffRoadAudioManager.SkidClip;
        skidAudio.loop = true;
        skidAudio.playOnAwake = false;
        skidAudio.volume = 0f;
        skidAudio.spatialBlend = 0.8f;
        skidAudio.minDistance = 2f;
        skidAudio.maxDistance = 28f;

        impactAudio = gameObject.AddComponent<AudioSource>();
        impactAudio.playOnAwake = false;
        impactAudio.spatialBlend = 0.85f;
        impactAudio.minDistance = 2f;
        impactAudio.maxDistance = 35f;
    }

    private void Update()
    {
        if (!aiControlled)
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool accelerate = heldControls[(int)OffRoadVehicleControl.Accelerate]
                || (keyboard != null && (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
                || (gamepad != null && gamepad.buttonSouth.isPressed);
            bool brake = heldControls[(int)OffRoadVehicleControl.Brake]
                || (keyboard != null && (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed))
                || (gamepad != null && gamepad.buttonEast.isPressed);
            float horizontal = (heldControls[(int)OffRoadVehicleControl.SteerRight] ? 1f : 0f)
                - (heldControls[(int)OffRoadVehicleControl.SteerLeft] ? 1f : 0f);
            if (keyboard != null)
                horizontal += (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            if (gamepad != null)
                horizontal += gamepad.leftStick.ReadValue().x;

            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float requestedThrottle = accelerate && !brake ? 1f : 0f;
            float requestedBrake = 0f;
            if (brake)
            {
                if (forwardSpeed > 1f)
                    requestedBrake = 1f;
                else
                    requestedThrottle = -1f;
            }

            SetInput(requestedThrottle, horizontal, requestedBrake,
                heldControls[(int)OffRoadVehicleControl.Handbrake]
                || (keyboard != null && keyboard.spaceKey.isPressed)
                || (gamepad != null && gamepad.leftShoulder.isPressed));
        }

        float speed = SpeedKmh;
        float revs = Mathf.Clamp01(speed / maxSpeedKmh * 0.72f + Mathf.Abs(throttle) * 0.48f);
        EngineRpm = Mathf.Lerp(850f, 6200f, revs);
        CurrentGear = speed < 1.5f && throttle < 0f ? -1 : Mathf.Clamp(1 + Mathf.FloorToInt(speed / 31f), 1, 6);
        if (engineAudio != null)
        {
            engineAudio.pitch = Mathf.Lerp(0.78f, 2.6f, revs);
            float engineLoad = Mathf.Max(Mathf.Abs(throttle), NormalizedSpeed * 0.55f);
            engineAudio.volume = Mathf.Lerp(0.2f, 0.46f, engineLoad);
        }

        if (roadAudio != null)
        {
            roadAudio.volume = Mathf.Lerp(0f, 0.32f, Mathf.Clamp01(speed / 95f));
            roadAudio.pitch = Mathf.Lerp(0.8f, 1.5f, NormalizedSpeed);
        }

        if (skidAudio != null)
        {
            skidAudio.volume = IsSkidding ? Mathf.Lerp(0.12f, 0.48f, NormalizedSpeed) : 0f;
            if (IsSkidding && !skidAudio.isPlaying)
                skidAudio.Play();
            else if (!IsSkidding && skidAudio.isPlaying)
                skidAudio.Stop();
        }
    }

    private void FixedUpdate()
    {
        if (controlsLocked)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].motorTorque = 0f;
                wheels[i].brakeTorque = maxBrakeTorque;
                UpdateWheelVisual(i);
                var emission = dust[i].emission;
                emission.rateOverTime = 0f;
            }
            IsSkidding = false;
            return;
        }

        float steeringLimit = maxSteerAngle * Mathf.Lerp(1f, 0.42f, NormalizedSpeed);
        wheels[0].steerAngle = steer * steeringLimit;
        wheels[1].steerAngle = steer * steeringLimit;

        Vector3 groundNormal = Vector3.zero;
        int groundedWheels = 0;
        for (int i = 0; i < wheels.Length; i++)
        {
            WheelHit groundHit;
            if (wheels[i].GetGroundHit(out groundHit))
            {
                groundNormal += groundHit.normal;
                groundedWheels++;
            }
        }

        if (groundedWheels > 0)
        {
            groundNormal.Normalize();
            Vector3 rightingAxis = Vector3.Cross(transform.up, groundNormal);
            body.AddTorque(rightingAxis * uprightAssist, ForceMode.Acceleration);
            body.AddTorque(groundNormal * (steer * steeringAssist * Mathf.Clamp01(SpeedKmh / 18f)),
                ForceMode.Acceleration);
            Vector3 lateralVelocity = Vector3.Project(body.linearVelocity, transform.right);
            body.AddForce(-lateralVelocity * 2.4f, ForceMode.Acceleration);
        }

        if (!aiControlled && Vector3.Dot(transform.up, Vector3.up) < 0.15f)
        {
            overturnedTime += Time.fixedDeltaTime;
            if (overturnedTime >= 1.5f)
            {
                ResetVehicle();
                return;
            }
        }
        else
        {
            overturnedTime = 0f;
        }

        bool reverse = throttle < -0.01f;
        float signedTorque = reverse ? maxMotorTorque * 0.42f * throttle : maxMotorTorque * throttle;
        float rollingVelocity = Vector3.Dot(body.linearVelocity, transform.forward);
        bool brakingAgainstMotion = brake > 0.01f || (reverse && rollingVelocity > 1.1f) || (!reverse && throttle > 0f && rollingVelocity < -1f);
        float brakeTorque = brake * maxBrakeTorque + (handbrake ? maxBrakeTorque * 0.72f : 0f);
        if (brakingAgainstMotion)
        {
            brakeTorque = Mathf.Max(brakeTorque, maxBrakeTorque * 0.78f);
            signedTorque = 0f;
        }
        if (!reverse && SpeedKmh > maxSpeedKmh)
            signedTorque = 0f;
        body.AddForce(-body.linearVelocity * body.linearVelocity.magnitude * 0.0012f, ForceMode.Force);
        if (SpeedKmh < maxSpeedKmh || reverse)
        {
            Vector3 driveDirection = groundedWheels > 0
                ? Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (driveDirection.sqrMagnitude > 0.01f)
                body.AddForce(driveDirection * throttle * accelerationAssist, ForceMode.Acceleration);
        }

        bool skidding = false;
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].motorTorque = signedTorque * 0.25f;
            wheels[i].brakeTorque = brakeTorque * (handbrake && i >= 2 ? 1.6f : 1f);
            UpdateWheelVisual(i);
            WheelHit hit;
            bool grounded = wheels[i].GetGroundHit(out hit);
            float slip = grounded ? Mathf.Abs(hit.sidewaysSlip) + Mathf.Abs(hit.forwardSlip) * 0.65f : 0f;
            skidding |= grounded && slip > 0.72f;
            var emission = dust[i].emission;
            emission.rateOverTime = grounded && (slip > 0.42f || (NormalizedSpeed > 0.45f && throttle > 0.3f))
                ? Mathf.Lerp(8f, 34f, NormalizedSpeed)
                : 0f;
        }

        IsSkidding = skidding;
        ApplyAntiRoll(0, 1);
        ApplyAntiRoll(2, 3);
    }

    private void ApplyAntiRoll(int left, int right)
    {
        WheelHit hit;
        bool groundedLeft = wheels[left].GetGroundHit(out hit);
        float travelLeft = groundedLeft ? (-wheels[left].transform.InverseTransformPoint(hit.point).y - wheels[left].radius)
            / wheels[left].suspensionDistance : 1f;
        bool groundedRight = wheels[right].GetGroundHit(out hit);
        float travelRight = groundedRight ? (-wheels[right].transform.InverseTransformPoint(hit.point).y - wheels[right].radius)
            / wheels[right].suspensionDistance : 1f;
        float force = (travelLeft - travelRight) * 6200f;
        if (groundedLeft)
            body.AddForceAtPosition(wheels[left].transform.up * -force, wheels[left].transform.position);
        if (groundedRight)
            body.AddForceAtPosition(wheels[right].transform.up * force, wheels[right].transform.position);
    }

    private void UpdateWheelVisual(int index)
    {
        Vector3 position;
        Quaternion rotation;
        wheels[index].GetWorldPose(out position, out rotation);
        wheelVisuals[index].SetPositionAndRotation(position, rotation);
    }

    public void SetInput(float newThrottle, float newSteer, float newBrake, bool newHandbrake)
    {
        if (controlsLocked)
        {
            throttle = 0f;
            steer = 0f;
            brake = 1f;
            handbrake = false;
            return;
        }

        throttle = Mathf.Clamp(newThrottle, -1f, 1f);
        float steeringResponse = aiControlled ? 5f : 3.5f;
        steer = Mathf.MoveTowards(steer, Mathf.Clamp(newSteer, -1f, 1f),
            steeringResponse * Time.deltaTime);
        brake = Mathf.Clamp01(newBrake);
        handbrake = newHandbrake;
    }

    public void SetControlHeld(OffRoadVehicleControl control, bool isHeld)
    {
        heldControls[(int)control] = isHeld;
    }

    private void OnDisable()
    {
        for (int i = 0; i < heldControls.Length; i++)
            heldControls[i] = false;
        if (body != null)
            SetInput(0f, 0f, 1f, false);
    }

    public void SetStyle(bool buggy)
    {
        buggyStyle = buggy;
        if (chassisVisual == null)
            return;
        chassisVisual.localScale = new Vector3(1.9f, 0.67f, buggy ? 3.1f : 3.8f);
        cabinVisual.localScale = new Vector3(1.52f, buggy ? 0.58f : 0.78f, buggy ? 1.2f : 1.65f);
        cabinVisual.localPosition = new Vector3(0f, buggy ? 1.43f : 1.70f, -0.25f);
    }

    public void ResetVehicle()
    {
        controlsLocked = false;
        for (int i = 0; i < heldControls.Length; i++)
            heldControls[i] = false;
        SetInput(0f, 0f, 1f, false);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        body.Sleep();
        body.WakeUp();
    }

    public void StopForRaceEnd()
    {
        controlsLocked = true;
        SetInput(0f, 0f, 1f, false);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        if (roadAudio != null)
            roadAudio.volume = 0f;
        if (skidAudio != null)
            skidAudio.Stop();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude < 8f || bodyMaterial == null)
            return;

        if (Time.time - lastImpactTime > 0.18f && impactAudio != null && OffRoadAudioManager.ImpactClip != null)
        {
            float impactVolume = Mathf.Clamp01((collision.relativeVelocity.magnitude - 6f) / 28f);
            impactAudio.PlayOneShot(OffRoadAudioManager.ImpactClip, impactVolume);
            lastImpactTime = Time.time;
        }

        damage = Mathf.Clamp01(damage + collision.relativeVelocity.magnitude * 0.004f);
        bodyMaterial.color = Color.Lerp(bodyMaterial.color, new Color(0.22f, 0.20f, 0.18f), damage * 0.35f);
    }

    private GameObject CreatePart(PrimitiveType type, string partName, Vector3 localPosition,
        Vector3 localScale, Material material, bool collider)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider)
            Destroy(part.GetComponent<Collider>());
        return part;
    }

    private static void SetFriction(WheelCollider wheel, float forward, float sideways)
    {
        WheelFrictionCurve forwardCurve = wheel.forwardFriction;
        forwardCurve.extremumSlip = 0.42f;
        forwardCurve.extremumValue = 1f;
        forwardCurve.asymptoteSlip = 0.85f;
        forwardCurve.asymptoteValue = 0.72f;
        forwardCurve.stiffness = forward;
        wheel.forwardFriction = forwardCurve;
        WheelFrictionCurve sideCurve = wheel.sidewaysFriction;
        sideCurve.extremumSlip = 0.3f;
        sideCurve.extremumValue = 1f;
        sideCurve.asymptoteSlip = 0.65f;
        sideCurve.asymptoteValue = 0.75f;
        sideCurve.stiffness = sideways;
        wheel.sidewaysFriction = sideCurve;
    }

    private static Material CreateMaterial(string materialName, Color color, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName, color = color };
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        return material;
    }

    private static AudioClip CreateEngineClip()
    {
        const int sampleRate = 22050;
        const int sampleCount = sampleRate;
        float[] samples = new float[sampleCount];
        float filteredExhaust = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float phase = i / (float)sampleRate * 56f * Mathf.PI * 2f;
            float firing = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * 4f)), 10f);
            filteredExhaust = Mathf.Lerp(filteredExhaust, firing, 0.12f);
            float engineWave = Mathf.Sin(phase) * 0.46f + Mathf.Sin(phase * 2f) * 0.28f
                + Mathf.Sin(phase * 3f) * 0.16f + Mathf.Sin(phase * 4f) * 0.1f;
            samples[i] = engineWave * 0.15f + filteredExhaust * 0.16f;
        }
        OffRoadAudioManager.SmoothLoopEndpoints(samples);
        AudioClip clip = AudioClip.Create("Procedural off-road engine", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
