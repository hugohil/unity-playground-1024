using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float smoothTime = 0.1f;
    
    [Header("Orbit Settings")]
    [SerializeField] private bool useOrbitMode = false;
    [SerializeField] private float orbitDistance = 10f;
    [SerializeField] private Vector3 orbitCenter = Vector3.zero;
    
    [Header("Key Bindings")]
    [SerializeField] private KeyCode moveForwardKey = KeyCode.W;
    [SerializeField] private KeyCode moveBackwardKey = KeyCode.S;
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [SerializeField] private KeyCode moveUpKey = KeyCode.E;
    [SerializeField] private KeyCode moveDownKey = KeyCode.Q;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode toggleCursorKey = KeyCode.Escape;
    [SerializeField] private KeyCode toggleOrbitKey = KeyCode.Tab;
    
    [Header("Mouse Settings")]
    [SerializeField] private bool invertMouseY = false;
    [SerializeField] private bool invertMouseX = false;
    [SerializeField] private float scrollSensitivity = 1f;
    
    // Internal variables
    private Vector3 currentVelocity;
    private float rotationX = 0f;
    private float rotationY = 0f;
    
    private void Start()
    {
        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (useOrbitMode)
        {
            // Initialize orbit rotation based on current position
            Vector3 direction = transform.position - orbitCenter;
            rotationX = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            rotationY = Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;
        }
        else
        {
            // Initialize FPS rotation based on current rotation
            Vector3 rotation = transform.rotation.eulerAngles;
            rotationX = rotation.y;
            rotationY = -rotation.x;
        }
    }

    private void Update()
    {
        HandleRotation();
        
        if (useOrbitMode)
            HandleOrbitMovement();
        else
            HandleFPSMovement();
            
        // Toggle cursor lock
        if (Input.GetKeyDown(toggleCursorKey))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? 
                              CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = !Cursor.visible;
        }
        
        // Toggle orbit mode
        if (Input.GetKeyDown(toggleOrbitKey))
        {
            SetOrbitMode(!useOrbitMode);
        }
    }
    
    private void HandleRotation()
    {
        // Get mouse input with inversion options
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * (invertMouseX ? -1 : 1);
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertMouseY ? -1 : 1);
        
        // Update rotation angles
        rotationX += mouseX;
        rotationY = Mathf.Clamp(rotationY - mouseY, -89f, 89f);
    }
    
    private void HandleFPSMovement()
    {
        // Apply rotation
        transform.rotation = Quaternion.Euler(-rotationY, rotationX, 0f);
        
        // Calculate input vector based on key bindings
        Vector3 input = Vector3.zero;
        
        if (Input.GetKey(moveForwardKey)) input.z += 1f;
        if (Input.GetKey(moveBackwardKey)) input.z -= 1f;
        if (Input.GetKey(moveRightKey)) input.x += 1f;
        if (Input.GetKey(moveLeftKey)) input.x -= 1f;
        if (Input.GetKey(moveUpKey)) input.y += 1f;
        if (Input.GetKey(moveDownKey)) input.y -= 1f;
        
        // Normalize input vector to prevent faster diagonal movement
        if (input.magnitude > 1f)
            input.Normalize();
        
        // Calculate movement vector
        Vector3 move = transform.right * input.x + transform.up * input.y + transform.forward * input.z;
        
        // Apply sprint multiplier if sprint key is held
        float currentSpeed = Input.GetKey(sprintKey) ? moveSpeed * sprintMultiplier : moveSpeed;
        
        // Move with smoothing
        transform.position = Vector3.SmoothDamp(
            transform.position,
            transform.position + move * currentSpeed,
            ref currentVelocity,
            smoothTime
        );
    }
    
    private void HandleOrbitMovement()
    {
        // Calculate new position based on orbit
        float x = orbitDistance * Mathf.Sin(rotationX * Mathf.Deg2Rad) * Mathf.Cos(rotationY * Mathf.Deg2Rad);
        float y = orbitDistance * Mathf.Sin(rotationY * Mathf.Deg2Rad);
        float z = orbitDistance * Mathf.Cos(rotationX * Mathf.Deg2Rad) * Mathf.Cos(rotationY * Mathf.Deg2Rad);
        
        Vector3 targetPosition = orbitCenter + new Vector3(x, y, z);
        
        // Adjust orbit distance with scroll wheel and sensitivity
        orbitDistance = Mathf.Max(0.1f, orbitDistance - Input.mouseScrollDelta.y * scrollSensitivity);
        
        // Move to new position with smoothing
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        
        // Look at orbit center
        transform.LookAt(orbitCenter);
    }
    
    // Public methods
    public void SetOrbitMode(bool orbit)
    {
        useOrbitMode = orbit;
        
        if (orbit)
        {
            // Store current look direction for orbit initialization
            Vector3 direction = transform.position - orbitCenter;
            rotationX = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            rotationY = Mathf.Asin(direction.y / direction.magnitude) * Mathf.Rad2Deg;
        }
    }
    
    public void SetOrbitCenter(Vector3 center)
    {
        orbitCenter = center;
    }
    
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }
    
    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = sensitivity;
    }
}