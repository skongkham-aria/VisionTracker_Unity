using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Subscribes to the VisionTrackerUnity event and visualizes the tracked objects
/// using a pool of UI prefabs on a screen-space canvas.
/// THIS IS A MORE ROBUST VERSION that avoids script execution order issues.
/// </summary>
public class ObjectVisualizer : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject with the VisionTrackerUnity script here.")]
    [SerializeField] private VisionTrackerUnity visionTracker;

    [Tooltip("The UI prefab for the bounding box and its label.")]
    [SerializeField] private GameObject boundingBoxPrefab;

    [Tooltip("The RectTransform of the parent UI element that displays the webcam feed (e.g., a RawImage or the entire Canvas).")]
    [SerializeField] private RectTransform displayArea;

    // Object pool to avoid creating/destroying UI elements every frame
    private List<GameObject> boxPool = new List<GameObject>();
    private int activeBoxes = 0;

    // We will now get this reference when it's first needed, not in Start()
    private WebCamTexture webcamTexture;
    private bool hasTriedToGetTexture = false;

    void OnEnable()
    {
        if (visionTracker != null)
        {
            visionTracker.OnObjectsTracked += HandleObjectsTracked;
        }
    }

    void OnDisable()
    {
        if (visionTracker != null)
        {
            visionTracker.OnObjectsTracked -= HandleObjectsTracked;
        }
    }

    private void HandleObjectsTracked(VisionTrackerUnity.TrackedObject[] objects)
    {
        // --- FIX: Try to get the webcam texture if we haven't already ---
        // This is safer than using Start()
        if (!hasTriedToGetTexture)
        {
            var webcamDisplay = visionTracker.GetComponent<WebcamDisplay>();
            if (webcamDisplay != null && webcamDisplay.WebcamTexture != null && webcamDisplay.WebcamTexture.isPlaying)
            {
                webcamTexture = webcamDisplay.WebcamTexture;
                Debug.Log("ObjectVisualizer successfully got webcam texture reference.");
            }
            hasTriedToGetTexture = true; // Only try this once for efficiency
        }

        // If we still don't have the texture, we can't do anything.
        if (webcamTexture == null) return;
        // -----------------------------------------------------------------

        activeBoxes = objects.Length;

        while (boxPool.Count < activeBoxes)
        {
            GameObject newBox = Instantiate(boundingBoxPrefab, displayArea);
            boxPool.Add(newBox);
        }

        for (int i = 0; i < activeBoxes; i++)
        {
            boxPool[i].SetActive(true);
            UpdateBoxVisuals(boxPool[i], objects[i]);
        }

        for (int i = activeBoxes; i < boxPool.Count; i++)
        {
            boxPool[i].SetActive(false);
        }
    }

    private void UpdateBoxVisuals(GameObject boxInstance, VisionTrackerUnity.TrackedObject obj)
    {
        // This check is still good to have, but the main fix is in the handler
        if (webcamTexture == null || displayArea == null) return;

        RectTransform boxRect = boxInstance.GetComponent<RectTransform>();
        TextMeshProUGUI label = boxInstance.GetComponentInChildren<TextMeshProUGUI>();

        // Coordinate Conversion
        float displayWidth = displayArea.rect.width;
        float displayHeight = displayArea.rect.height;

        float widthRatio = displayWidth / webcamTexture.width;
        float heightRatio = displayHeight / webcamTexture.height;

        float boxWidth = obj.width * widthRatio;
        float boxHeight = obj.height * heightRatio;
        boxRect.sizeDelta = new Vector2(boxWidth, boxHeight);

        boxRect.anchorMin = Vector2.zero;
        boxRect.anchorMax = Vector2.zero;
        boxRect.pivot = Vector2.zero;

        float posX = obj.x * widthRatio;
        float posY = displayHeight - (obj.y * heightRatio) - boxHeight;
        boxRect.anchoredPosition = new Vector2(posX, posY);

        // Update the label text
        label.text = $"ID: {obj.id} | D: {obj.depth:F2}m | C: {obj.confidence:F2}";
    }
}