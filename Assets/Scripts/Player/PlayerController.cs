using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
CURRENT BUGS
- slopes are detected as stairs
- can't walk up stairs when pushing against wall
- step check can break jump
- general bugginess
- messy code
- model/camera lerping should get faster if distance is large
*/

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
    public float stepHeight = 0.625f; // distance from ground to top of step
    public float stepClearance = 0.15f; // clearance above ground of check collider
    public float stepSpeedTolerance = 1f; // speed above which step will take place

    [Header("View Settings")]
    public float mouseSensitivity = 100.0f;
    public float clampAngle = 80.0f;
    public float cameraLerpSpeed = 10f;

    private GroundCollisionChecker _groundChecker;

    private Rigidbody _rigidbody;
    private Vector3 _inputDir = Vector3.zero;
    private bool _jump = false;

    private Camera _camera;
    private float _cameraHeight;
    private float _rotY = 0.0f; // rotation around the up/y axis
    private float _rotX = 0.0f; // rotation around the right/x axis

    RaycastHit[] _raycastHits;
    Collider[] _hitColliders;

    public float PlayerHeight
    {
        get
        {
            float min = groundCollider.transform.position.y - groundCollider.radius;
            float max = bodyCollider.transform.position.y + bodyCollider.height * 0.5f; 

            return max - min;
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

        // Lerp modelRoot to rigidbody position
        Vector3 targetPos = transform.position;
        float maxMove = cameraLerpSpeed * Time.deltaTime;
        float reqMove = targetPos.y - modelRoot.transform.position.y;
        float t = (reqMove != 0f) ? Mathf.Clamp01(maxMove / Mathf.Abs(reqMove)) : 1f;
        modelRoot.transform.position = Vector3.Lerp(modelRoot.transform.position, targetPos, t);
    }

    void FixedUpdate()
    {
        bool hasInput = (_inputDir.x != 0f || _inputDir.z != 0f);
        bool onGround = _groundChecker.OnGround || _groundChecker.CheckSphere();

        Transform mt = modelRoot.transform;

        Vector3 currentVelocity = _rigidbody.velocity;
        currentVelocity.y = 0.0f; // don't compensate for gravity
        float currentSpeed = currentVelocity.magnitude; // needs to be x, z only!

        Vector3 inputDirWS = mt.right * _inputDir.x + mt.forward * _inputDir.z;
        Vector3 checkDir = (currentSpeed > stepSpeedTolerance) ? currentVelocity.normalized : inputDirWS;
        Vector3 groundNormal = GetGroundNormal(transform.position);

        // Basic movement
        if (hasInput)
        {
            Vector3 moveDir = Vector3.ProjectOnPlane(inputDirWS, groundNormal);

            // Don't boost *down* slopes
            Vector3 targetVelocity = (moveDir.y > 0) ? moveDir * maxSpeed : inputDirWS * maxSpeed;

            Vector3 diffVelocity = targetVelocity - currentVelocity;
            Vector3 force = diffVelocity * acceleration * _rigidbody.mass;

            if (!onGround)
            {
                force *= airSpeedMultiplier;
            }
            
            _rigidbody.AddForce(force);
        }

        // Check for step
        if (currentSpeed > stepSpeedTolerance || hasInput)
        {
            Vector3 newPosition;
            if (CanStepUp(checkDir, out newPosition))
            {
                float offsetY = newPosition.y - _rigidbody.position.y;
                if (offsetY > stepHeight * 0.2f)
                {
                    // teleport rigidbody, zero y component to avoid bouncing or sinking
                    _rigidbody.position = newPosition;
                    _rigidbody.velocity = currentVelocity;

                    // This will be lerped up in Update()
                    Vector3 rootPos = mt.position;
                    rootPos.y -= offsetY;
                    mt.position = rootPos;
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

    Vector3 GetGroundNormal (Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 0.1f;
        Vector3 rayEnd = position - Vector3.up * 0.1f;
        Vector3 groundNormal = Vector3.up;

        int layerMask = LayerMask.GetMask("World", "WorldDynamic");

        float rayLength = Mathf.Abs(rayStart.y - rayEnd.y);
        int numHits = Physics.RaycastNonAlloc(rayStart, Vector3.down, _raycastHits, rayLength, layerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < numHits; ++i)
        {
            RaycastHit hit = _raycastHits[i];

            if (hit.point.y > rayEnd.y)
            {
                rayEnd.y = hit.point.y;
                groundNormal = hit.normal;
            }
        }

        return groundNormal;
    }

    bool CanStepUp (Vector3 dir, out Vector3 newPosition)
    {
        Transform mt = transform;
        Quaternion checkRotation = Quaternion.LookRotation(dir, Vector3.up);
        newPosition = Vector3.zero;

        // Get Colliders in step detection zone
        Vector3 boxHalfSize = new Vector3(bodyCollider.radius, stepHeight - stepClearance, bodyCollider.radius) * 0.5f;
        Vector3 boxOffset = Vector3.up * (stepHeight - boxHalfSize.y) + dir * (boxHalfSize.z + 0.05f);

        int layerMask = LayerMask.GetMask("World", "WorldDynamic");

        int numHits = Physics.OverlapBoxNonAlloc(mt.position + boxOffset, boxHalfSize, _hitColliders,
            checkRotation, layerMask, QueryTriggerInteraction.Ignore);

        if (numHits < 1)
        {
            return false;
        }

        // Find the closest point on each collider to a target
        Vector3 targetPoint = mt.position + boxOffset + Vector3.up * boxHalfSize.y + dir * boxHalfSize.z;
        Vector3 highestPoint = mt.position;
        float highestY = mt.position.y + stepClearance;
        bool validPoint = false;

        for (int i = 0; i < numHits; ++i)
        {
            Collider collider = _hitColliders[i];
            Vector3 point = Physics.ClosestPoint(targetPoint, collider, collider.transform.position, collider.transform.rotation);
            if (point.y > highestY && point.y < mt.position.y + stepHeight)
            {
                highestY = point.y;
                highestPoint = point;
                validPoint = true;
            }
        }

        if (!validPoint)
        {
            return false;
        }

        // Check we can move up to this step in current move dir
        float radius = bodyCollider.radius * 0.5f;
        float height = PlayerHeight;
        float heightTolerance = 0.05f;
        Vector3 p1 = new Vector3(mt.position.x, highestY + radius + heightTolerance, mt.position.z) + dir * (0.2f + radius);
        Vector3 p2 = new Vector3(p1.x, highestY + height - radius - heightTolerance, p1.z);

        if (!Physics.CheckCapsule(p1, p2, radius, layerMask, QueryTriggerInteraction.Ignore))
        {
            //newPosition = p1 - Vector3.up * radius;
            newPosition = mt.position;
            newPosition.y = highestY + heightTolerance;
            return true;
        }

        return false;
    }

    void DrawClosestPointGizmo (Vector3 dir)
    {
        Transform mt = transform;
        Quaternion checkRotation = Quaternion.LookRotation(dir, Vector3.up);

        // Get Colliders in step detection zone
        Vector3 boxHalfSize = new Vector3(bodyCollider.radius, stepHeight - stepClearance, bodyCollider.radius) * 0.5f;
        Vector3 boxOffset = Vector3.up * (stepHeight - boxHalfSize.y) + dir * (boxHalfSize.z + 0.05f);

        // Draw the collider
        Matrix4x4 localMatrix = Matrix4x4.identity;
        localMatrix.SetTRS(transform.position, checkRotation, Vector3.one);
        Gizmos.matrix = localMatrix;
        Vector3 boxLocalPosition = new Vector3(0f, boxOffset.y, boxHalfSize.z + 0.05f);
        Gizmos.DrawWireCube(boxLocalPosition, boxHalfSize * 2f);
        Gizmos.matrix = Matrix4x4.identity;

        int layerMask = LayerMask.GetMask("World", "WorldDynamic");

        int numHits = Physics.OverlapBoxNonAlloc(mt.position + boxOffset, boxHalfSize, _hitColliders,
            checkRotation, layerMask, QueryTriggerInteraction.Ignore);

        Vector3 targetPoint = mt.position + boxOffset + Vector3.up * boxHalfSize.y + dir * boxHalfSize.z;

        if (numHits < 1)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(targetPoint, 0.05f);

            // Try raycasting to find a smaller step
            Vector3 rayStart = targetPoint - Vector3.up * (stepHeight - stepClearance + 0.05f);
            Vector3 rayEnd = targetPoint - Vector3.up * stepHeight;
            Vector3 groundNormal = Vector3.up;

            float rayLength = Mathf.Abs(rayStart.y - rayEnd.y);
            int rayHits = Physics.RaycastNonAlloc(rayStart, Vector3.down, _raycastHits, rayLength, layerMask, QueryTriggerInteraction.Ignore);

            if (rayHits > 0)
            {
                for (int i = 0; i < rayHits; ++i)
                {
                    RaycastHit hit = _raycastHits[i];

                    if (hit.point.y > rayEnd.y)
                    {
                        rayEnd.y = hit.point.y;
                        groundNormal = hit.normal;
                    }
                }

                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, rayEnd);
                Gizmos.DrawWireSphere(rayEnd, 0.05f);
            }
        }

        Gizmos.color = Color.yellow;

        // Find the closest point on each collider to a target
        Vector3 highestPoint = mt.position;
        float highestY = mt.position.y + stepClearance;
        bool validPoint = false;

        for (int i = 0; i < numHits; ++i)
        {
            Collider collider = _hitColliders[i];
            Vector3 point = Physics.ClosestPoint(targetPoint, collider, collider.transform.position, collider.transform.rotation);
            if (point.y > highestY && point.y < mt.position.y + stepHeight)
            {
                highestY = point.y;
                highestPoint = point;
                validPoint = true;
            }
            
            Gizmos.DrawWireSphere(point, 0.1f);
        }

        if (!validPoint)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(highestPoint, 0.1f);

        // Check we can move up to this step in current move dir
        float radius = bodyCollider.radius * 0.5f;
        float height = PlayerHeight;
        float heightTolerance = 0.05f;
        Vector3 p1 = new Vector3(mt.position.x, highestY + radius + heightTolerance, mt.position.z) + dir * (0.2f + radius);
        Vector3 p2 = new Vector3(p1.x, highestY + height - radius - heightTolerance, p1.z);

        if (!Physics.CheckCapsule(p1, p2, radius, layerMask, QueryTriggerInteraction.Ignore))
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(p1, radius);
            Gizmos.DrawWireSphere(p2, radius);
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
            DrawClosestPointGizmo(checkDir);
        }
    }
}
