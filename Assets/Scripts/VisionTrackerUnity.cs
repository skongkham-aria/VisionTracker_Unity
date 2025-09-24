using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Unity wrapper for the VisionTracker C++ library.
/// This version correctly handles RGBA-to-BGR color conversion and matches the C-style API.
/// </summary>
public class VisionTrackerUnity : MonoBehaviour
{
    [Header("Model Paths")]
    [Tooltip("Model file names must be relative to the StreamingAssets folder.")]
    [SerializeField] private string yoloModelPath = "yolo11m-seg.onnx";
    [SerializeField] private string depthModelPath = "vits_qint8_sim_OP15.onnx";

    [Header("Input Source")]
    [Tooltip("Drag the GameObject that has the WebcamDisplay script here.")]
    [SerializeField] private WebcamDisplay webcamSource;

    [Header("Processing Settings")]
    [SerializeField] private bool processEveryFrame = true;
    [SerializeField] private float processInterval = 0.1f; // Process 10 times per second

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // This struct MUST exactly match the layout of the TrackedObject struct in the C++ API.
    [StructLayout(LayoutKind.Sequential)]
    public struct TrackedObject
    {
        public int id;
        public int classId;
        public float confidence;
        public float depth;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    // --- C++ Library Imports ---
    // These function signatures now correctly match the 'extern "C"' declarations in VisionTrackerAPI.h
    private const string DllName = "VisionTracker";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int InitializeTracker(string yoloPath, string depthPath);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ProcessFrame(IntPtr imageData, int width, int height);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetTrackedObjectCount();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void GetTrackedObjects(IntPtr objects, int maxCount);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Cleanup();


    public event Action<TrackedObject[]> OnObjectsTracked;
    private bool isInitialized = false;
    private float lastProcessTime = 0f;

    // Buffers for efficient pixel processing
    private Color32[] rgbaPixelData;
    private byte[] bgrPixelData;

    void Start()
    {
        InitializeVisionTracker();
    }

    void Update()
    {
        // Ensure everything is ready before trying to process a frame
        if (!isInitialized || webcamSource == null || webcamSource.WebcamTexture == null || !webcamSource.WebcamTexture.isPlaying)
        {
            return;
        }

        if (processEveryFrame || Time.time - lastProcessTime >= processInterval)
        {
            ProcessCameraFrame();
            lastProcessTime = Time.time;
        }
    }

    void OnDestroy()
    {
        if (isInitialized)
        {
            Debug.Log("Cleaning up VisionTracker resources...");
            Cleanup();
            isInitialized = false;
        }
    }

    private void InitializeVisionTracker()
    {
        try
        {
            string yoloFullPath = Path.Combine(Application.streamingAssetsPath, yoloModelPath);
            string depthFullPath = Path.Combine(Application.streamingAssetsPath, depthModelPath);

            Debug.Log($"Attempting to initialize VisionTracker with:\n- YOLO: {yoloFullPath}\n- Depth: {depthFullPath}");

            int result = InitializeTracker(yoloFullPath, depthFullPath);

            if (result == 0)
            {
                isInitialized = true;
                Debug.Log("VisionTracker initialized successfully!");
            }
            else
            {
                Debug.LogError($"Failed to initialize VisionTracker. The C++ library returned error code: {result}. Check C++ logs for model loading errors.");
            }
        }
        catch (DllNotFoundException)
        {
            Debug.LogError($"FATAL: '{DllName}.dll' not found. Please ensure the compiled C++ library is in the Assets/Plugins folder.");
        }
        catch (Exception e)
        {
            Debug.LogError($"An unexpected exception occurred during VisionTracker initialization: {e.Message}");
        }
    }

    private void ProcessCameraFrame()
    {
        WebCamTexture sourceTexture = webcamSource.WebcamTexture;

        // Initialize or resize our processing buffers if the texture size changes
        int pixelCount = sourceTexture.width * sourceTexture.height;
        if (rgbaPixelData == null || rgbaPixelData.Length != pixelCount)
        {
            rgbaPixelData = new Color32[pixelCount];
            bgrPixelData = new byte[pixelCount * 3]; // 3 bytes per pixel for BGR
        }

        // Get the pixel data from the webcam texture (Unity provides it as RGBA)
        sourceTexture.GetPixels32(rgbaPixelData);

        // --- CRITICAL STEP: Convert from Unity's RGBA to the required BGR format ---
        ConvertRgbaToBgr(rgbaPixelData, bgrPixelData);

        // Pin the correctly formatted BGR data in memory and send it to the C++ library
        GCHandle handle = GCHandle.Alloc(bgrPixelData, GCHandleType.Pinned);
        try
        {
            ProcessFrame(handle.AddrOfPinnedObject(), sourceTexture.width, sourceTexture.height);

            // Now, retrieve the results from the C++ library
            int objectCount = GetTrackedObjectCount();
            if (objectCount > 0)
            {
                TrackedObject[] trackedObjects = new TrackedObject[objectCount];
                GCHandle objectsHandle = GCHandle.Alloc(trackedObjects, GCHandleType.Pinned);
                try
                {
                    GetTrackedObjects(objectsHandle.AddrOfPinnedObject(), objectCount);
                    OnObjectsTracked?.Invoke(trackedObjects);

                    if (showDebugInfo)
                    {
                        LogTrackedObjects(trackedObjects);
                    }
                }
                finally
                {
                    objectsHandle.Free(); // Always unpin the memory for the objects
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"An error occurred while calling the C++ library: {e.Message}");
        }
        finally
        {
            handle.Free(); // ALWAYS unpin the memory for the image data
        }
    }

    /// <summary>
    /// Efficiently converts a Color32 array (RGBA) to a byte array in BGR format.
    /// </summary>
    private void ConvertRgbaToBgr(Color32[] input, byte[] output)
    {
        for (int i = 0; i < input.Length; i++)
        {
            int byteIndex = i * 3;
            output[byteIndex] = input[i].b; // Blue
            output[byteIndex + 1] = input[i].g; // Green
            output[byteIndex + 2] = input[i].r; // Red
        }
    }

    /// <summary>
    /// Formats and prints the details of tracked objects to the console.
    /// </summary>
    private void LogTrackedObjects(TrackedObject[] objects)
    {
        Debug.Log($"--- Tracked {objects.Length} objects this frame ---");
        foreach (var obj in objects)
        {
            Debug.Log($"  - ID: {obj.id}, Class: {obj.classId}, Conf: {obj.confidence:F2}, Depth: {obj.depth:F2}m, Box: ({obj.x:F0},{obj.y:F0} {obj.width:F0}x{obj.height:F0})");
        }
    }
}

