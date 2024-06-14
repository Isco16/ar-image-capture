using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

[RequireComponent(typeof(ImageTargetBehaviour), typeof(DefaultObserverEventHandler))]
public class ImageTargetContent : MonoBehaviour
{
    #region PROPERTIES
    [Tooltip("Augmented reality content parent.")]
    public GameObject arContent;
    [Tooltip("The factor that determines the maximum camera distance.\n" +
        "This value is multiplied by the minimum camera distance required to capture this target.")] 
    [Range(2f,5f)]
    public float maxDistanceFactor = 5f;
    [Tooltip("List of renderers to apply the captured texture.")]
    public Renderer[] targetRenderers;
    [Tooltip("Display a frame around the target bounds.")]
    public bool drawImageFrame;
    [Tooltip("Frame prefab to instantiate.")]
    public GameObject linePrefab;

    [Tooltip("Image resolution in X dimension.")]
    public int xAxisDimension;
    [Tooltip(".")]
    public int yAxisDimension;

    float minDistance;
    float maxDistance;
    Vector3[] worldCornersPos;
    
    ImageTargetBehaviour itb;
    DefaultObserverEventHandler observer;
    LineRenderer targetFrame;

    #endregion

    #region MONOBEHAVIOUR INHERITED MEMBERS 

    IEnumerator Start()
    {
        GetRenderers();

        arContent.SetActive(false);

        observer = GetComponent<DefaultObserverEventHandler>();
        observer.OnTargetFound.AddListener(OnTargetFound);
        observer.OnTargetLost.AddListener(OnTargetLost);

        yield return new WaitUntil(delegate(){ 
            return VuforiaBehaviour.Instance.VideoBackground != null;
        });

        yield return new WaitUntil(delegate () {
            return VuforiaBehaviour.Instance.VideoBackground.VideoBackgroundTexture != null;
        });

        yield return new WaitUntil(delegate () {
            return ImageTargetCapture.instance.arCam != null;
        });

        float horizontalFOV = Camera.VerticalToHorizontalFieldOfView(ImageTargetCapture.instance.arCam.fieldOfView, ImageTargetCapture.instance.arCam.aspect);
        float baseAngle = horizontalFOV / 2f;
        float FOVradians = baseAngle * Mathf.Deg2Rad;
        minDistance = (itb.GetSize().x / 2f) / Mathf.Tan(FOVradians);
        maxDistance = minDistance * maxDistanceFactor;

    }

    private void OnDestroy()
    {
        observer.OnTargetFound.RemoveListener(OnTargetFound);
        observer.OnTargetLost.RemoveListener(OnTargetLost);
    }

    private void Update()
    {
        print(itb.GetRuntimeTargetTexture());

    }

    #endregion

    #region PRIVATE MEMBERS

    void OnTargetFound()
    {
        ImageTargetCapture.instance?.OnTargetFound(itb.TargetName);
    }

    void OnTargetLost()
    {
        ControlArContent(false);
        ImageTargetCapture.instance?.OnTargetLost();
    }

    void GetRenderers()
    {
        MeshRenderer[] meshes = arContent.GetComponentsInChildren<MeshRenderer>();
        SkinnedMeshRenderer[] skinnedMeshes = arContent.GetComponentsInChildren<SkinnedMeshRenderer>();
        List<Renderer> renderers = new List<Renderer>();

        if (meshes.Length > 0)
            renderers.AddRange(meshes);

        if (skinnedMeshes.Length > 0)
            renderers.AddRange(skinnedMeshes);

        targetRenderers = renderers.ToArray();

        meshes = null;
        skinnedMeshes = null;
        renderers.Clear();
    }

    void InstantiateLineRenderer()
    {
        GameObject obj = GameObject.Instantiate(linePrefab);
        obj.transform.parent = transform;
        targetFrame = obj.GetComponent<LineRenderer>();
    }

    #endregion

    #region PUBLIC MEMBERS

    /// <summary>
    /// Gets the minimum distance range for this target.
    /// </summary>
    /// <returns>Min distance float value.</returns>
    public float GetMinDistance()
    {
        return minDistance;
    }

    /// <summary>
    /// Gets the maximum distance range for this target.
    /// </summary>
    /// <returns>Max distance float value.</returns>
    public float GetMaxDistance()
    {
        return maxDistance;
    }

    /// <summary>
    /// Gets the Image Target Behaviour component.
    /// </summary>
    /// <returns>ImageTargetBehaviour object reference</returns>
    public ImageTargetBehaviour GetImageTargetBehaviour()
    {
        if (!itb)
            itb = GetComponent<ImageTargetBehaviour>();
        return itb;
    }

    /// <summary>
    /// Gets the world corner positions based on the image target's local dimensions.
    /// </summary>
    /// <returns>Target's 3D world corner positions.</returns>
    public Vector3[] GetWorldCornersPositions()
    {
        Vector2 size = itb.GetSize();
        float width = size.x / 2f;
        float height = size.y / 2f;
        worldCornersPos = new Vector3[4];
        worldCornersPos[0] = transform.TransformPoint(width * -1, 0f, height);
        worldCornersPos[1] = transform.TransformPoint(width, 0f, height);
        worldCornersPos[2] = transform.TransformPoint(width * -1, 0f, height * -1);
        worldCornersPos[3] = transform.TransformPoint(width, 0f, height * -1);
        return worldCornersPos;
    }

    /// <summary>
    /// Activate or deactivate the AR content's parent depending on the <paramref name="value"/> passed in.
    /// </summary>
    /// <param name="value">Passing a true value activates the content, while passing a false value deactivates the content.</param>
    public void ControlArContent(bool value)
    {
        arContent.SetActive(value);
    }

    /// <summary>
    /// Applies the given <paramref name="capture"/> texture to all objects inside the renderer list.
    /// </summary>
    /// <param name="capture">The texture to apply to.</param>
    public void ApplyCaptureToRenderers(Texture2D capture)
    {
        foreach (Renderer rend in targetRenderers)
            rend.material.SetTexture("_MainTex",capture);
    }

    /// <summary>
    /// Instantiates and displays the frame around the target's bound.
    /// </summary>
    public void DrawImageTargetFrame()
    {
        Vector3[] arrengedPoints = new Vector3[worldCornersPos.Length];
        arrengedPoints[0] = worldCornersPos[0];
        arrengedPoints[1] = worldCornersPos[1];
        arrengedPoints[2] = worldCornersPos[3];
        arrengedPoints[3] = worldCornersPos[2];
        if (!targetFrame)
            InstantiateLineRenderer();
        ControlFrame(true);
        targetFrame.SetPositions(arrengedPoints);
    }

    /// <summary>
    /// Controls the activation or deactivation of the target frame object in the scene
    /// </summary>
    /// <param name="activate"></param>
    public void ControlFrame(bool activate)
    {
        if(targetFrame.gameObject.activeSelf != activate)
            targetFrame.gameObject.SetActive(activate);
    }

    #endregion
}
