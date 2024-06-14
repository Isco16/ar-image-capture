using UnityEngine;
using UnityEngine.UI;
using Vuforia;

public class CameraImageAccess : Singleton<CameraImageAccess>
{
    #region PRIVATE_MEMBERS

#if UNITY_EDITOR
    PixelFormat mPixelFormat = PixelFormat.RGB888; // Editor passes in a RGBA8888 texture instead of RGB888
#else
    PixelFormat mPixelFormat = PixelFormat.RGB888; // Use RGB888 for mobile
#endif
    private bool mFormatRegistered = false;
    private Texture2D texture;

    private int width;
    private int height;

    #endregion // PRIVATE_MEMBERS

    #region MONOBEHAVIOUR MEMBERS

    void Start()
    {
        // Register Vuforia life-cycle callbacks:
        VuforiaApplication.Instance.OnVuforiaStarted += RegisterFormat;
        VuforiaApplication.Instance.OnVuforiaPaused += OnPause;
        //VuforiaBehaviour.Instance.World.OnStateUpdated += GetCameraTexture;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // Unregister Vuforia life-cycle callbacks:
        VuforiaApplication.Instance.OnVuforiaStarted -= RegisterFormat;
        VuforiaApplication.Instance.OnVuforiaPaused -= OnPause;
        //VuforiaBehaviour.Instance.World.OnStateUpdated -= GetCameraTexture;
    }

    #endregion

    #region PUBLIC MEMBERS
    /// 
    /// Called each time the Vuforia state is updated
    /// 
    public Texture GetCameraTexture()
    {
        if (mFormatRegistered)
        {
            texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            Vuforia.Image image = VuforiaBehaviour.Instance.CameraDevice.GetCameraImage(mPixelFormat);
            image.CopyBufferToTexture(texture);
            texture.Apply();


            Debug.Log(
                "\nImage Format: " + image.PixelFormat +
                "\nImage Size: " + image.Width + " x " + image.Height +
                "\nBuffer Size: " + image.BufferWidth + " x " + image.BufferHeight +
                "\nImage Stride: " + image.Stride + "\n"
            );
        }

        return texture;
    }

    #endregion

    #region PRIVATE MEMBERS

    /// 
    /// Called when app is paused / resumed
    /// 
    void OnPause(bool paused)
    {
        if (paused)
        {
            Debug.Log("App was paused");
            UnregisterFormat();
        }
        else
        {
            Debug.Log("App was resumed");
            RegisterFormat();
        }
    }
    /// 
    /// Register the camera pixel format
    /// 
    void RegisterFormat()
    {
        // Vuforia has started, now register camera image format
        bool success = VuforiaBehaviour.Instance.CameraDevice.SetFrameFormat(mPixelFormat, true);
        if (success)

        {
            Debug.Log("Successfully registered pixel format " + mPixelFormat.ToString());
            mFormatRegistered = true;
        }
        else
        {
            Debug.LogError(
                "Failed to register pixel format " + mPixelFormat.ToString() +
                "\n the format may be unsupported by your device;" +
                "\n consider using a different pixel format.");
            mFormatRegistered = false;
        }
    }
    /// 
    /// Unregister the camera pixel format (e.g. call this when app is paused)
    /// 
    void UnregisterFormat()
    {
        Debug.Log("Unregistering camera pixel format " + mPixelFormat.ToString());
        VuforiaBehaviour.Instance.CameraDevice.SetFrameFormat(mPixelFormat, false);
        mFormatRegistered = false;
    }

    #endregion
}