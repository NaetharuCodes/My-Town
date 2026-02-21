using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 20f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 0.5f;
    public float minZoom = 5f;
    public float maxZoom = 70f;

    [Header("Map Bounds")]
    public float mapMinX = 0f;
    public float mapMaxX = 128f;
    public float mapMinY = 0f;
    public float mapMaxY = 128f;

    private Camera cam;
    private CameraInputActions inputActions;
    private Vector2 panInput;

    void Awake()
    {
        cam = GetComponent<Camera>();
        inputActions = new CameraInputActions();
    }

    void OnEnable()
    {
        inputActions.Camera.Enable();

        // Subscribe to the zoom action
        inputActions.Camera.Zoom.performed += OnZoom;
    }

    void OnDisable()
    {
        inputActions.Camera.Zoom.performed -= OnZoom;
        inputActions.Camera.Disable();
    }

    void Update()
    {
        HandlePan();
        ClampPosition();
    }

    void HandlePan()
    {
        panInput = inputActions.Camera.Pan.ReadValue<Vector2>();

        if (panInput != Vector2.zero)
        {
            float adjustedSpeed = panSpeed * (cam.orthographicSize / maxZoom);
            Vector3 move = new Vector3(panInput.x, panInput.y, 0f);
            transform.position += move.normalized * adjustedSpeed * Time.deltaTime;
        }
    }

    void OnZoom(InputAction.CallbackContext context)
    {
        float scrollValue = context.ReadValue<float>();
        cam.orthographicSize -= scrollValue * zoomSpeed;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
    }

    void ClampPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, mapMinX, mapMaxX);
        pos.y = Mathf.Clamp(pos.y, mapMinY, mapMaxY);
        transform.position = pos;
    }
}