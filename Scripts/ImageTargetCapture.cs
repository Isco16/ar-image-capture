using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Vuforia;
using UnityEngine.Events;

public class ImageTargetCapture : Singleton<ImageTargetCapture>
{
    #region PROPERTIES

    // Image Capture Settings
    [Tooltip("Vuforia AR camera.")]
    public Camera arCam;
    [Tooltip("Maximum camera movement permitted for image capture.")]
    public float maxCameraShakeRange;
    int shakeValuesLength = 20;
    List<float> shakeValuesList;

    // Image Target Capture Events
    [Space(4f)]
    [Header("Events")]
    [Space(4f)]
    public UnityEvent onTargetFound;
    public UnityEvent onTargetLost;
    public UnityEvent onTargetMeetReqts;
    public UnityEvent onTargetLostReqts;
    public UnityEvent onTargetCaptured;

    // Image Tracking State Data
    enum AR_State { OnTargetLost, OnTargetFound, ImageCaptured }
    AR_State arState;
    bool targetMeetsReqts;

    // Image Targets Data
    Dictionary<string, ImageTargetContent> arTargetContents;
    ImageTargetContent currentTarget;
    string currentTargetId;

    // Vuforia Camera Background
    Transform cameraBackTransform;
    MeshFilter cameraBackMesh;
    MeshRenderer cameraBackRenderer;

    #endregion

    #region MONOBEHAVIOUR INHERITED MEMBERS

    protected override void Awake()
    {
        base.Awake();

        List<ImageTargetContent> imageTargets = new List<ImageTargetContent>();
        imageTargets.AddRange(FindObjectsOfType<ImageTargetContent>());

        arTargetContents = new Dictionary<string, ImageTargetContent>();

        foreach (ImageTargetContent itc in imageTargets)
            arTargetContents.Add(itc.GetImageTargetBehaviour().TargetName, itc);

        arCam = VuforiaBehaviour.Instance.GetComponent<Camera>();
        arState = AR_State.OnTargetLost;
        
        Resources.UnloadUnusedAssets();
    }

    private IEnumerator Start()
    {
        GameObject plane = default(GameObject);

        yield return new WaitUntil(delegate() {
            plane = null;
            if (VuforiaBehaviour.Instance.transform.childCount > 0)
                plane = VuforiaBehaviour.Instance.transform.GetChild(0).gameObject;
            return plane; 
        }) ;

        cameraBackTransform = plane.transform;
        cameraBackMesh = cameraBackTransform.gameObject.GetComponent<MeshFilter>();
        cameraBackRenderer = cameraBackTransform.gameObject.GetComponent<MeshRenderer>();
        shakeValuesList = new List<float>();

        Camera.onPostRender += OnPostRenderCallback;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Camera.onPostRender -= OnPostRenderCallback;
    }

    #endregion

    #region IMAGE TRACKING STATES MEMBERS

    public void OnTargetFound(string trgIdx)
    {
        currentTargetId = trgIdx;
        currentTarget = arTargetContents[currentTargetId];
        onTargetFound.Invoke();
        arState = AR_State.OnTargetFound;
    }

    public void OnTargetLost()
    {
        if (!AnyImageTargetTracked())
        {
            currentTargetId = string.Empty;
            currentTarget = null;
            arState = AR_State.OnTargetLost;
        }

        onTargetLost.Invoke();
        Resources.UnloadUnusedAssets();
    }

    void OnPostRenderCallback(Camera cam)
    {
        switch (arState)
        {
            case AR_State.OnTargetLost:
                ResetValidationValues();
                break;
            case AR_State.OnTargetFound:
                OnTargetFoundBehaviour();
                break;
            case AR_State.ImageCaptured:
                ImageCapturedBehaviour();
                break;
        }
    }

    bool AnyImageTargetTracked()
    {
        bool isAny = false;

        foreach (KeyValuePair<string, ImageTargetContent> itc in arTargetContents)
        {
            if (!itc.Value) continue;
            if (itc.Value.GetImageTargetBehaviour().TargetStatus.Status == Status.TRACKED)
            {
                isAny = true;
                break;
            }
        }

        return isAny;
    }

    void ResetValidationValues()
    {
        if (shakeValuesList.Count > 0)
            shakeValuesList.Clear();

        targetMeetsReqts = false;
    }

    /// <summary>
    /// FUNCTION WHICH DETERMINE WHETHER TO TAKE A CAPTURE OR NOT
    /// </summary>
    void OnTargetFoundBehaviour()
    {
        bool targetMeetingReqts = IsTargetMeetingRequirements(arCam, currentTarget);
        if (currentTarget.drawImageFrame)
            currentTarget.DrawImageTargetFrame();

        if (targetMeetsReqts != targetMeetingReqts)
        {
            print("targetMeetingReqts: " + targetMeetingReqts.ToString());
            if (targetMeetingReqts)
                onTargetMeetReqts.Invoke();
            else
                onTargetLostReqts.Invoke();
        }

        targetMeetsReqts = targetMeetingReqts;
    }

    /// <summary>
    /// This method is called after the target image has been captured
    /// </summary>
    void ImageCapturedBehaviour()
    {
        if (currentTarget.drawImageFrame)
            currentTarget.ControlFrame(false);
    }

    #endregion

    #region IMAGE CAPTURE MEMBERS

    /// <summary>
    /// This method captures the image target section only if all requirements are met.
    /// </summary>
    public void CaptureArImage()
    {
        if (!targetMeetsReqts) return;
        Texture cameraTex = CameraImageAccess.instance.GetCameraTexture();
        Vector2[] projectedPoints = Convert3DPointsTo2DCoor(cameraTex.width, cameraTex.height);
        Texture2D capture = OpenCVImageCapture.instance.CaptureImageTargetTexture(cameraTex, projectedPoints);

        currentTarget.ControlArContent(true);
        currentTarget.ApplyCaptureToRenderers(capture);
        onTargetCaptured?.Invoke();
        arState = AR_State.ImageCaptured;
    }

    Vector2[] Convert3DPointsTo2DCoor(int imageWidth, int imageHeight)
    {
        if (!cameraBackTransform) return null;

        Vector3[] proyectedPoints = GetProyectedPoints();
        Vector2[] cornerPoints2D = new Vector2[4];

        for (int i = 0; i < cornerPoints2D.Length; i++)
        {
            cornerPoints2D[i] = PlaneCoordinates(cameraBackTransform, i, proyectedPoints[i], cameraBackMesh.mesh.vertices[2], cameraBackMesh.mesh.vertices[1] * 2f);
        }

        if (!CheckValid2dPoints(cornerPoints2D, imageWidth, imageHeight))
            cornerPoints2D = null;

        return cornerPoints2D;
    }

    Vector3[] GetProyectedPoints()
    {
        if (!cameraBackTransform) return null;

        Vector3 cameraPoint = arCam.transform.position;
        Vector3 planePoint = cameraBackTransform.position;
        Vector3 planeNormDir = cameraBackTransform.TransformDirection(Vector3.up).normalized;
        Vector3[] cornersPos = currentTarget.GetWorldCornersPositions();
        Vector3[] worldVertices = new Vector3[cornersPos.Length];
        Vector3[] proyectedPoints = new Vector3[cornersPos.Length];

        for (int i = 0; i < cornersPos.Length; i++)
        {
            Vector3 cornerPoint = cornersPos[i];

            float x = (((planePoint.y - cornerPoint.y) * planeNormDir.y + (planePoint.z - cornerPoint.z) * planeNormDir.z + planeNormDir.x * planePoint.x) * cameraPoint.x -
                cornerPoint.x * ((planePoint.y - cameraPoint.y) * planeNormDir.y + (planePoint.z - cameraPoint.z) * planeNormDir.z + planeNormDir.x * planePoint.x))
                /
                ((cameraPoint.x - cornerPoint.x) * planeNormDir.x + (cameraPoint.y - cornerPoint.y) * planeNormDir.y + (cameraPoint.z - cornerPoint.z) * planeNormDir.z);

            float y = (((planePoint.x - cornerPoint.x) * planeNormDir.x + (planePoint.z - cornerPoint.z) * planeNormDir.z + planeNormDir.y * planePoint.y) * cameraPoint.y -
                cornerPoint.y * ((planePoint.x - cameraPoint.x) * planeNormDir.x + (planePoint.z - cameraPoint.z) * planeNormDir.z + planeNormDir.y * planePoint.y))
                /
                ((cameraPoint.y - cornerPoint.y) * planeNormDir.y + (cameraPoint.x - cornerPoint.x) * planeNormDir.x + (cameraPoint.z - cornerPoint.z) * planeNormDir.z);

            float z = (((planePoint.y - cornerPoint.y) * planeNormDir.y + (planePoint.x - cornerPoint.x) * planeNormDir.x + planeNormDir.z * planePoint.z) * cameraPoint.z -
                cornerPoint.z * ((planePoint.y - cameraPoint.y) * planeNormDir.y + (planePoint.x - cameraPoint.x) * planeNormDir.x + planeNormDir.z * planePoint.z))
                /
                ((cameraPoint.z - cornerPoint.z) * planeNormDir.z + (cameraPoint.y - cornerPoint.y) * planeNormDir.y + (cameraPoint.x - cornerPoint.x) * planeNormDir.x);

            float k = ((cameraPoint.x - planePoint.x) * planeNormDir.x + (cameraPoint.y - planePoint.y) * planeNormDir.y + (cameraPoint.z - planePoint.z) * planeNormDir.z)
                /
                ((cameraPoint.x - cornerPoint.x) * planeNormDir.x + (cameraPoint.y - cornerPoint.y) * planeNormDir.y + (cameraPoint.z - cornerPoint.z) * planeNormDir.z);

            Vector3 projectedPoint = new Vector3(x, y, z);

            proyectedPoints[i] = projectedPoint;


#if(UNITY_EDITOR)
            Debug.DrawRay(cornersPos[i], projectedPoint, Color.green);
            worldVertices[i] = cameraBackTransform.TransformPoint(cameraBackMesh.mesh.vertices[i]);
            Debug.DrawLine(worldVertices[i], projectedPoint, Color.red);
#endif
        }

        return proyectedPoints;
    }

    Vector2 PlaneCoordinates(Transform planeTrans, int vertexIdx, Vector3 worldPoint, Vector3 originWorldPoint, Vector3 size)
    {
        Vector3 planePoint = (planeTrans.InverseTransformPoint(worldPoint) - originWorldPoint);
        float xSize = cameraBackRenderer.sharedMaterial.mainTexture.width;
        float ySize = cameraBackRenderer.sharedMaterial.mainTexture.height;
        Vector2 point2d = new Vector2((planePoint.x / size.x) * xSize, (planePoint.z / size.z) * ySize);

        return point2d;
    }

    bool CheckValid2dPoints(Vector2[] points, int imageWidth, int imageHeight)
    {
        bool valid = true;

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].x < 0f || points[i].x > imageWidth || points[i].y < 0f || points[i].y > imageHeight)
            {
                valid = false;
                break;
            }
        }

        return valid;
    }

    #endregion

    #region IMAGE CAPTURE VALIDATION MEMBERS

    bool IsTargetMeetingRequirements(Camera arCamera, ImageTargetContent arTarget)
    {
        bool targetFullyVisible = IsTargetFullyVisible(arTarget);

        bool facingTarget = IsCameraFacingTarget(arCamera.transform, arTarget.transform, -0.5f) ;

        bool withinRange = IsCameraWithinRange(arCamera.transform, arTarget.transform, arTarget.GetMinDistance(), arTarget.GetMaxDistance());

        bool cameraStill = IsCameraStill();

        return facingTarget && withinRange && targetFullyVisible && cameraStill;
    }

    bool IsTargetFullyVisible(ImageTargetContent arTarget)
    {
        bool allVisible = true;

        Vector3[] cornersPos = arTarget.GetWorldCornersPositions();

        for (int i = 0; i < cornersPos.Length; i++)
        {
            Vector3 viewportPos = Camera.main.WorldToViewportPoint(cornersPos[i]);

            if (viewportPos.x < 0f || viewportPos.x > 1f || viewportPos.y < 0f || viewportPos.y > 1f)
            {
                allVisible = false;
                break;
            }
        }

        return allVisible;
    }

    bool IsCameraFacingTarget(Transform obj1, Transform obj2, float range)
    {
        Vector3 rotation1 = obj1.forward;
        Vector3 rotation2 = obj2.up;
        float dotProd = Vector3.Dot(rotation1, rotation2);
        bool isFacing = dotProd < range;
        return isFacing;
    }

    bool IsCameraWithinRange(Transform obj1, Transform obj2, float minDist, float maxDist)
    {
        float distance = Vector3.Distance(obj1.position, obj2.position);
        bool withinRange = distance < maxDist && distance > minDist;
        return withinRange;
    }

    bool IsCameraStill()
    {
        bool stillCamera = false;
        float distance = Vector3.Distance(arCam.transform.position, currentTarget.transform.position);
        shakeValuesList.Add(distance);

        if (shakeValuesList.Count >= shakeValuesLength)
        {
            if(shakeValuesList.Count > shakeValuesLength)
                shakeValuesList.RemoveAt(0);

            float[] deltas = new float[shakeValuesList.Count-1];

            for(int i = 1; i < deltas.Length; i++)
            {
                deltas[i] = Mathf.Abs(shakeValuesList[i] - shakeValuesList[i - 1]);
            }

            float maxValue = deltas.Max();

            if (maxValue <= maxCameraShakeRange)
                stillCamera = true;
            else
                shakeValuesList.Clear();
        }

        return stillCamera;
    }

    #endregion
}
