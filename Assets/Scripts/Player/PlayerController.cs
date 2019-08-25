using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
TODO
- if onGround but no bottom ray hit, use rigidbody bottom
- more resistance from sliding down slopes < slopeTolerance
- should probably add terminal velocity check

CURRENT BUGS
- should I just be using collision with the bodyCollider to detect steps?
- slopes are detected as stairs
- can't walk up stairs when pushing against wall
- step check can break jump
- general bugginess
- messy code
- model/camera lerping should get faster if distance is large
*/

public struct RayInfo
{
    public bool hit;
    public Vector3 point;
    public Vector3 normal;

    public RayInfo (Vector3 point, Vector3 normal)
    {
        this.hit = true;
        this.point = point;
        this.normal = normal;
    }
}

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    public GameObject modelRoot;
    public SphereCollider groundCollider;
    public CapsuleCollider bodyCollider;

    [Header("Movement Settings")]
    public float maxSpeed = 10f;
    public float airSpeedMultiplier = 0.75f;
    public float acceleration = 8f;
    public float jumpForce = 400f;
    public float stepHeight = 0.625f; // Distance from ground to top of step
    public float stepClearance = 0.15f; // Clearance above ground of check collider
    public float stepSpeedTolerance = 1f; // Speed above which step will take place
    public float stepMaxSlopeAngle = 25f; // When stepping up, do not step onto highly slanted surfaces
    public float maxSlopeAngle = 45f;

    [Header("View Settings")]
    public float mouseSensitivity = 100.0f;
    public float clampAngle = 85.0f;
    public float cameraLerpSpeed = 5f;

    protected GroundCollisionChecker _groundChecker;

    protected Rigidbody _rigidbody;
    protected Vector3 _inputDir = Vector3.zero;
    protected bool _jump = false;

    protected Camera _camera;
    protected float _cameraHeight;
    protected float _playerHeight;
    protected float _rotY = 0.0f; // rotation around the up/y axis
    protected float _rotX = 0.0f; // rotation around the right/x axis
    protected float _maxSlopeAngleArc;
    protected float _stepMaxSlopeAngleArc;
    protected float _maxRayLength;
    protected const float _rayTolerance = 0.2f;
    protected RayInfo _hitFront;
    protected RayInfo _hitBottom;

    protected RaycastHit[] _raycastHits;
    protected Collider[] _hitColliders;

    public float PlayerHeight
    {
        get
        {
            return _playerHeight;
        }
    }

    void Awake ()
    {
        _raycastHits = new RaycastHit[8];
        _hitColliders = new Collider[8];
        _rigidbody = GetComponent<Rigidbody>();
        _camera = GetComponentInChildren<Camera>();
        _groundChecker = GetComponent<GroundCollisionChecker>();

        Vector3 euler = transform.localRotation.eulerAngles;
        _rotY = euler.y;
        _rotX = euler.x;

        UpdateSlopeSettings();
        UpdatePlayerHeight();
    }

    void OnValidate ()
    {
        UpdateSlopeSettings();
        UpdatePlayerHeight();
    }

    void UpdatePlayerHeight ()
    {
        float min = groundCollider.transform.position.y - groundCollider.radius;
        float max = bodyCollider.transform.position.y + bodyCollider.height * 0.5f; 
        _playerHeight =  max - min;        
    }

    void UpdateSlopeSettings ()
    {
        // Calculate max ray length from center of ground collider based on slope angle
        _stepMaxSlopeAngleArc = Mathf.Sin(Mathf.PI * 0.5f - stepMaxSlopeAngle * Mathf.Deg2Rad);
        _maxSlopeAngleArc = Mathf.Sin(Mathf.PI * 0.5f - maxSlopeAngle * Mathf.Deg2Rad);
        _maxRayLength = groundCollider.radius / _maxSlopeAngleArc + _rayTolerance;          
    }

    bool RaycastGround (ref RayInfo rayInfo, Vector3 rayStart, float rayLength, int layerMask = Physics.DefaultRaycastLayers)
    {
        rayInfo.hit = false;

        int numHits = Physics.RaycastNonAlloc(rayStart, Vector3.down, _raycastHits, rayLength, layerMask, QueryTriggerInteraction.Ignore);

        if (numHits > 0)
        {
            rayInfo.hit = true;
            float maxY = rayStart.y - rayLength;

            for (int i = 0; i < numHits; ++i)
            {
                RaycastHit hit = _raycastHits[i];
                if (hit.point.y > maxY)
                {
                    rayInfo.point = hit.point;
                    rayInfo.normal = hit.normal;
                    maxY = hit.point.y;
                }
            }
        }

        return rayInfo.hit;
    }

    bool StepCheck (ref Vector3 point, Vector3 dir, int layerMask = Physics.DefaultRaycastLayers)
    {
        bool validPoint = false;

        // Get Colliders in step detection zone
        float forwardOffset = 0.05f;
        Vector3 boxHalfSize = new Vector3(bodyCollider.radius, stepHeight - stepClearance, bodyCollider.radius) * 0.5f;
        Vector3 boxOffset = Vector3.up * (stepHeight - boxHalfSize.y) + dir * (boxHalfSize.z + forwardOffset);

        Quaternion checkRotation = Quaternion.LookRotation(dir, Vector3.up);
        int numHits = Physics.OverlapBoxNonAlloc(transform.position + boxOffset, boxHalfSize, _hitColliders,
            checkRotation, layerMask, QueryTriggerInteraction.Ignore);

        // Find the closest point on each collider to a target
        Vector3 targetPoint = transform.position + boxOffset + Vector3.up * boxHalfSize.y + dir * boxHalfSize.z;
        Vector3 highestPoint = targetPoint;
        highestPoint.y = transform.position.y;

        if (numHits > 0)
        {
            float limitY = transform.position.y + stepHeight;

            for (int i = 0; i < numHits; ++i)
            {
                Collider collider = _hitColliders[i];
                Vector3 closestPoint = Physics.ClosestPoint(targetPoint, collider, collider.transform.position, collider.transform.rotation);
                if (closestPoint.y > highestPoint.y && closestPoint.y <= limitY)
                {
                    highestPoint = closestPoint;
                    validPoint = true;
                }
            }
        }

        point = validPoint ? highestPoint : targetPoint;

        return validPoint;
    }

    bool CheckCapsule (Vector3 basePoint, float radius, float height, int layerMask)
    {
        Vector3 p1 = new Vector3(basePoint.x, basePoint.y + radius, basePoint.z);
        Vector3 p2 = new Vector3(basePoint.x, basePoint.y + height - radius, basePoint.z);

        return Physics.CheckCapsule(p1, p2, radius, layerMask, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        // Movement
        float z = Input.GetAxis("Forward");
        float x = Input.GetAxis("LeftRight");
        _inputDir = new Vector3(x, 0f, z);
        
        // Don't normalize small analogue movements
        if (_inputDir.magnitude > 1f) { _inputDir.Normalize(); }

        _jump = _jump || Input.GetButtonDown("Jump");

        // Aiming
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = -Input.GetAxis("Mouse Y");

        _rotY += mouseX * mouseSensitivity * Time.deltaTime;
        _rotX += mouseY * mouseSensitivity * Time.deltaTime;
        _rotX = Mathf.Clamp(_rotX, -clampAngle, clampAngle);

        modelRoot.transform.rotation = Quaternion.Euler(0.0f, _rotY, 0.0f);
        _camera.transform.rotation = Quaternion.Euler(_rotX, _rotY, 0.0f);

        // FIXME: Lerp modelRoot to rigidbody position
        Vector3 targetPos = transform.position;
        float maxMove = cameraLerpSpeed * Time.deltaTime;
        float reqMove = targetPos.y - modelRoot.transform.position.y;
        float t = (reqMove != 0f) ? Mathf.Clamp01(maxMove / Mathf.Abs(reqMove)) : 1f;
        modelRoot.transform.position = Vector3.Lerp(modelRoot.transform.position, targetPos, t);
    }

    void FixedUpdate()
    {
        int layerMask = LayerMask.GetMask("World", "WorldDynamic"); // FIXME: remove this

        bool hasInput = (_inputDir.x != 0f || _inputDir.z != 0f);
        bool onGround = _groundChecker.OnGround || _groundChecker.CheckSphere();

        Transform mt = modelRoot.transform;

        Vector3 currentVelocity = _rigidbody.velocity;
        currentVelocity.y = 0.0f; // don't compensate for gravity
        float currentSpeed = currentVelocity.magnitude; // needs to be x, z only!

        Vector3 inputDirWS = mt.right * _inputDir.x + mt.forward * _inputDir.z;
        Vector3 checkDir = (currentSpeed > stepSpeedTolerance) ? currentVelocity.normalized : inputDirWS;

        RaycastGround(ref _hitBottom, transform.position + Vector3.up * 0.05f, _maxRayLength, layerMask);
        Vector3 groundNormal = _hitBottom.hit ? _hitBottom.normal : Vector3.up;

        // Basic movement
        if (hasInput)
        {
            // TODO: Try multiplying y component by some slope factor to avoid too much slope boosting
            float dot = Vector3.Dot(groundNormal, Vector3.up);
            bool orientToSlope = (dot >= _maxSlopeAngleArc) ? true : false;
            Vector3 moveDir = Vector3.ProjectOnPlane(inputDirWS, groundNormal);

            // Don't boost *down* slopes, or up slopes that are too high
            Vector3 targetVelocity = (moveDir.y > 0 && orientToSlope) ? moveDir * maxSpeed : inputDirWS * maxSpeed;
            Vector3 diffVelocity = targetVelocity - currentVelocity;
            Vector3 force = diffVelocity * acceleration * _rigidbody.mass;

            if (!onGround)
            {
                force *= airSpeedMultiplier;
            }
            
            _rigidbody.AddForce(force);
        }

        // Handle moving up steps (either with adequate velocity, or input towards step)
        if (currentSpeed > stepSpeedTolerance || hasInput)
        {
            Vector3 forwardPoint = Vector3.zero;
            bool stepCheck = StepCheck(ref forwardPoint, checkDir, layerMask);
            forwardPoint += Vector3.up * 0.05f;
            RaycastGround(ref _hitFront, forwardPoint, stepHeight, layerMask);

            // if (stepCheck) { DebugDrawPoint(forwardPoint, 0.1f, Color.white, 0.5f, false); }

            float offsetY = _hitFront.point.y - transform.position.y;

            if (stepCheck && _hitFront.hit && offsetY > stepClearance)
            {
                // TODO: This needs some testing to be sure it works OK. Could cause issues on some weird geometry
                Vector3 hitBottom = _hitBottom.hit ? _hitBottom.point : transform.position;

                Debug.DrawLine(hitBottom, _hitFront.point, Color.red, 0.5f, false);
                Vector3 mid = hitBottom + (_hitFront.point - hitBottom) * 0.5f;
                DebugDrawPoint(mid, 0.1f, Color.yellow, 0.5f, false);

                Vector3 midOffset = (transform.position + Vector3.up * groundCollider.radius) - mid;
                float dot = Vector3.Dot(_hitFront.normal, Vector3.up);

                bool doStep = groundCollider.radius > midOffset.magnitude && dot >= _stepMaxSlopeAngleArc;

                //Debug.Log(string.Format("STEP: {0} | (r:{1}, d:{2})", doStep, groundCollider.radius, midOffset.magnitude));

                if (doStep)
                {
                    // Check the player's rigidbody can move up and forward before trying to step
                    float clearance = 0.05f;
                    float radius = bodyCollider.radius;
                    Vector3 basePointUp = new Vector3(transform.position.x, _hitFront.point.y + clearance, transform.position.z);
                    if (!CheckCapsule(basePointUp, radius, PlayerHeight + clearance, layerMask))
                    {
                        float checkRadius = radius * 0.25f;
                        float forwardOffset = radius - (checkRadius / radius) + 0.1f;
                        if (!CheckCapsule(basePointUp + checkDir * forwardOffset, checkRadius, PlayerHeight + clearance, layerMask))
                        {
                            offsetY = basePointUp.y - transform.position.y;

                            // teleport rigidbody, zero y component to avoid bouncing or sinking
                            _rigidbody.position = basePointUp;
                            _rigidbody.velocity = currentVelocity;

                            // This will be lerped up in Update()
                            Vector3 rootPos = mt.position;
                            rootPos.y -= offsetY;
                            mt.position = rootPos;
                        }
                    }
                }
            }
        }

        // Handle at end because velocity y possibly set to 0 on steps
        if (_jump)
        {
            if (onGround)
            {
                _rigidbody.AddForce(Vector3.up * jumpForce);
            }
            _jump = false;
        }
    }



    void OnDrawGizmos ()
    {
        if (Application.isPlaying)
        {

            bool hasInput = (_inputDir.x != 0f || _inputDir.z != 0f);

            Vector3 currentVelocity = _rigidbody.velocity;
            currentVelocity.y = 0f;
            float stepSpeedTolerance = 4f;
            float currentSpeed = currentVelocity.magnitude;

            Transform mt = modelRoot.transform;

            Vector3 moveDir = mt.right * _inputDir.x + mt.forward * _inputDir.z;
            Vector3 checkDir = (currentSpeed > stepSpeedTolerance) ? currentVelocity.normalized : (hasInput) ? moveDir.normalized : mt.forward;


            float forwardOffset = 0.05f;
            Vector3 boxHalfSize = new Vector3(bodyCollider.radius, stepHeight - stepClearance, bodyCollider.radius) * 0.5f;
            Vector3 boxOffset = Vector3.up * (stepHeight - boxHalfSize.y) + checkDir * (boxHalfSize.z + forwardOffset);
            Vector3 targetPoint = transform.position + boxOffset + Vector3.up * boxHalfSize.y + checkDir * boxHalfSize.z;

            Vector3 p1 = new Vector3(transform.position.x, transform.position.y + stepHeight, transform.position.z);
            Vector3 offset = Vector3.down * (boxHalfSize.y * 2f);
            Gizmos.DrawLine(p1, targetPoint);
            Gizmos.DrawLine(p1 + offset, targetPoint + offset);
        }
    }

    void DebugDrawPoint (Vector3 point, float size, Color color, float time = 0f, bool depthTest = true)
    {
        float halfSize = size * 0.5f;
        Debug.DrawLine(point - Vector3.right * halfSize, point + Vector3.right * halfSize, color, time, depthTest);
        Debug.DrawLine(point - Vector3.up * halfSize, point + Vector3.up * halfSize, color, time, depthTest);
        Debug.DrawLine(point - Vector3.forward * halfSize, point + Vector3.forward * halfSize, color, time, depthTest);
    }
}
