using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Initializes and displays a webcam feed on a RawImage UI element or a Renderer component.
/// </summary>
public class WebcamDisplay : MonoBehaviour
{
    [Header("Webcam Settings")]
    [Tooltip("Index of the webcam to use from the WebCamTexture.devices array.")]
    [SerializeField] private int webcamIndex = 0;
    [SerializeField] private int requestedWidth = 1920;
    [SerializeField] private int requestedHeight = 1080;
    [SerializeField] private int requestedFPS = 30;

    [Header("Display Components")]
    [Tooltip("Assign a RawImage component here to display the feed on a UI canvas.")]
    [SerializeField] private RawImage uiDisplay;
    [Tooltip("Assign a Renderer component (like on a Quad or Plane) to display the feed in the 3D scene.")]
    [SerializeField] private Renderer sceneDisplay;

    private WebCamTexture webcamTexture;
    public WebCamTexture WebcamTexture => webcamTexture; // Public property to allow other scripts to access the texture

    void Start()
    {
        InitializeWebcam();
    }

    private void InitializeWebcam()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No webcams found.");
            return;
        }

        if (webcamIndex >= devices.Length)
        {
            Debug.LogError($"Webcam index {webcamIndex} is out of bounds. Only {devices.Length} devices found.");
            webcamIndex = 0; // Default to the first camera
        }

        WebCamDevice device = devices[webcamIndex];
        Debug.Log($"Starting webcam: {device.name}");

        // Create the webcam texture
        webcamTexture = new WebCamTexture(device.name, requestedWidth, requestedHeight, requestedFPS);

        // Assign the texture to the display components
        if (uiDisplay != null)
        {
            uiDisplay.texture = webcamTexture;
        }
        if (sceneDisplay != null)
        {
            sceneDisplay.material.mainTexture = webcamTexture;
        }

        // Start the webcam
        webcamTexture.Play();
    }

    void OnDestroy()
    {
        // Stop the webcam when the object is destroyed
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}
