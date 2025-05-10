using UnityEngine;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Managing.Timing;
using FishNet.Transporting;
using FishNet.Utility.Template;
using static FishNet.Utility.Template.TickNetworkBehaviour;

public class Oyuncuv2 : TickNetworkBehaviour
{
    [Header("Movement Settings")]
    public float hareketHizi = 8f;
    public float jumpForce = 7f;
    public float playerHeight = 2f;
    public float groundDrag = 6f;
    public float airDrag = 1f;
    public float movementmultiplier = 1f;
    public float airmultiplier = 0.8f;

    [Header("Look Settings")]
    public float sensX = 100f;
    public float sensY = 100f;
    public float multiplier = 0.01f;

    [Header("Components")]
    public PredictionRigidbody predictionRigidbody; // Keep this as you're using its AddForce and Simulate
    private Rigidbody _rbInstance;
    private Camera _cam;

    private float _mouseX;
    private float _mouseY;
    private float _xRotation;
    private float _yRotation;
    private bool _isGrounded;
    private float _horizontalInput;
    private float _verticalInput;
    private Vector3 _currentMoveDir;
    private bool _jumpQueued;

    #region Prediction Data Structures
    public struct ReplicateInputData : IReplicateData
    {
        public Vector3 MoveDirection;
        public bool Jump;
        public Quaternion BodyRotation;

        private uint _tick;

        public ReplicateInputData(Vector3 moveDirection, bool jump, Quaternion bodyRotation)
        {
            MoveDirection = moveDirection;
            Jump = jump;
            BodyRotation = bodyRotation;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    public struct ReconcileStateData : IReconcileData // Adjusted back to explicit fields
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public float XRotationVisual; // For reconciling visual body rotation
        public float YRotationVisual; // For reconciling visual camera pitch

        private uint _tick;

        public ReconcileStateData(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity, float xRotVisual, float yRotVisual)
        {
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            XRotationVisual = xRotVisual;
            YRotationVisual = yRotVisual;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
    #endregion

    void Awake()
    {
        _cam = Camera.main;
        if (_cam == null)
        {
            Debug.LogError("Main Camera not found.", this);
            enabled = false;
            return;
        }

        _rbInstance = GetComponent<Rigidbody>();
        if (_rbInstance == null)
        {
            Debug.LogError("Rigidbody component not found on this object.", this);
            enabled = false;
            return;
        }

        if (predictionRigidbody == null) // Ensure predictionRigidbody is initialized
        {
            predictionRigidbody = new PredictionRigidbody();
        }
        predictionRigidbody.Initialize(_rbInstance); // Initialize with the actual Rigidbody

        if (base.Owner.IsLocalClient)
        {
            _cam.transform.SetParent(transform);
            _cam.transform.localPosition = new Vector3(0, playerHeight * 0.4f, 0.2f);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Camera localCam = GetComponentInChildren<Camera>();
            if (localCam != null && localCam != _cam)
            {
                localCam.gameObject.SetActive(false);
            }
        }
        _rbInstance.freezeRotation = true;
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        base.SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        if (!base.Owner.IsLocalClient && _cam != null && _cam.transform.parent == transform)
        {
            _cam.transform.SetParent(null);
        }
    }

    void Update()
    {
        if (!base.IsOwner) { return; }

        _isGrounded = Physics.Raycast(transform.position, Vector3.down, playerHeight / 2 + 0.2f);
        _rbInstance.linearDamping = _isGrounded ? groundDrag : airDrag;

        HandleInput();

        if (_cam != null)
        {
            _cam.transform.localRotation = Quaternion.Euler(_yRotation, 0, 0);
        }
        transform.rotation = Quaternion.Euler(0, _xRotation, 0);
    }

    void HandleInput()
    {
        _horizontalInput = Input.GetAxisRaw("Horizontal");
        _verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0;
        right.y = 0;
        _currentMoveDir = (forward.normalized * _verticalInput + right.normalized * _horizontalInput).normalized;

        _mouseX = Input.GetAxisRaw("Mouse X");
        _mouseY = Input.GetAxisRaw("Mouse Y");

        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            _jumpQueued = true;
        }

        _xRotation += _mouseX * sensX * multiplier;
        _yRotation -= _mouseY * sensY * multiplier;
        _yRotation = Mathf.Clamp(_yRotation, -90f, 90f);
    }

    protected override void TimeManager_OnTick()
    {
        if (base.IsOwner)
        {
            PerformReplicate(BuildInputData(), ReplicateState.Invalid); // Using Invalid as requested
        }

        if (base.IsServerStarted)
        {
            CreateReconcile();
        }
    }

    private ReplicateInputData BuildInputData()
    {
        if (!base.IsOwner) return default;
        ReplicateInputData inputs = new ReplicateInputData(
            _currentMoveDir,
            _jumpQueued,
            Quaternion.Euler(0, _xRotation, 0)
        );
        _jumpQueued = false;
        return inputs;
    }

    public override void CreateReconcile()
    {
        // Get current Rigidbody state directly
        ReconcileStateData stateData = new ReconcileStateData(
            _rbInstance.position,
            _rbInstance.rotation,
            _rbInstance.linearVelocity,
            _rbInstance.angularVelocity,
            _xRotation, // Current visual X rotation
            _yRotation  // Current visual Y rotation
        );
        PerformReconcile(stateData);
    }

    [Replicate]
    private void PerformReplicate(ReplicateInputData inputData, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        // Apply rotation to Rigidbody for physics simulation
        _rbInstance.MoveRotation(inputData.BodyRotation);

        Vector3 forceToApply = inputData.MoveDirection * hareketHizi;
        if (_isGrounded) // Consider re-evaluating _isGrounded based on Rigidbody state if replaying
        {
            predictionRigidbody.AddForce(forceToApply * movementmultiplier, ForceMode.Acceleration);
        }
        else
        {
            predictionRigidbody.AddForce(forceToApply * airmultiplier, ForceMode.Acceleration);
        }

        if (inputData.Jump)
        {
            predictionRigidbody.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        }
        predictionRigidbody.Simulate();
    }

    [Reconcile]
    private void PerformReconcile(ReconcileStateData stateData, Channel channel = Channel.Unreliable)
    {
        // Apply reconciled state directly to the Rigidbody instance
        _rbInstance.position = stateData.Position;
        _rbInstance.rotation = stateData.Rotation;
        _rbInstance.linearVelocity = stateData.Velocity;
        _rbInstance.angularVelocity = stateData.AngularVelocity;

        // Reconcile visual rotation values
        _xRotation = stateData.XRotationVisual;
        _yRotation = stateData.YRotationVisual;

        // Ensure transform matches exactly after reconcile (especially for non-owners or corrections)
        // For the owner, Update() will also set these, but this ensures server state is primary.
        transform.position = _rbInstance.position; // Redundant if _rbInstance.position already set, but ensures linkage
        transform.rotation = _rbInstance.rotation; // Redundant if _rbInstance.rotation already set

        if (_cam != null && base.IsOwner)
        {
            _cam.transform.localRotation = Quaternion.Euler(_yRotation, 0, 0);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 rayStart = new Vector3(transform.position.x, transform.position.y, transform.position.z);
        Gizmos.DrawLine(rayStart, rayStart + Vector3.down * (playerHeight / 2 + 0.2f));
    }
}