using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using System.Collections;

public class WebCamera : MonoBehaviour
{
    [SerializeField]
    private RawImage rawImage;
    [SerializeField]
    private TFLiteTest liteTest;
    [SerializeField] 
    private TextAsset haarCascasde;

    private WebCamTexture webCamTexture;
    private WebCamDevice[] devices;
    private CascadeClassifier cascade;
    private OpenCvSharp.Rect myFace;
    private Texture2D finalTexture;
    private Texture2D smallTexture;

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 60;
        devices = WebCamTexture.devices;

        foreach (var cam in devices)
        {
            if (cam.isFrontFacing)
            {
                webCamTexture = new WebCamTexture(cam.name);
                webCamTexture.deviceName = cam.name;
                break;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        rawImage.transform.Rotate(Vector3.forward, 90);
#endif
        rawImage.texture = webCamTexture;
        webCamTexture.Play();

        FileStorage storageFaces = new FileStorage(haarCascasde.text, FileStorage.Mode.Read | FileStorage.Mode.Memory);
        cascade = new CascadeClassifier();
        if (!cascade.Read(storageFaces.GetFirstTopLevelNode()))
            throw new System.Exception("FaceProcessor.Initialize: Failed to load faces cascade classifier");

        StartCoroutine(EmotionRoutine());
    }

    private IEnumerator EmotionRoutine()
    {
        finalTexture = new Texture2D(0, 0);
        myFace = new OpenCvSharp.Rect(0, 0, 48, 48);
        while (true)
        {
            CopyTexture(webCamTexture, finalTexture);
#if UNITY_ANDROID && !UNITY_EDITOR
            RotateImage(finalTexture, 90);
#endif
            Mat webImage = OpenCvSharp.Unity.TextureToMat(finalTexture);
            FindNewFace(webImage);
            Display(webImage);
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void FindNewFace(Mat frame)
    {
        var faces = cascade.DetectMultiScale(frame, 1.1, 2, HaarDetectionType.ScaleImage);
        
        if (faces.Length > 0)
        {
            myFace = faces[0];
        }
    }

    private void Display(Mat frame)
    {
        if (myFace != null)
        {
            frame.Rectangle(myFace, new Scalar(250, 0, 0), 2);
        }
        else return;

        if (webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
        {
            smallTexture = new Texture2D(myFace.Width, myFace.Height, TextureFormat.RGBA32, false);
            try
            {
                //smallTexture.SetPixels(finalTexture.GetPixels(myFace.X, finalTexture.height - myFace.Bottom, myFace.Width, myFace.Height));
                CopySectionTexture(finalTexture, smallTexture, myFace);
            }
            catch
            {
                return;
            }
            
            Scale(smallTexture, 48, 48);
            liteTest.ChangeSprite(smallTexture);
        }
    }

    private void Scale(Texture2D tex, int width, int height, FilterMode mode = FilterMode.Trilinear)
    {
        UnityEngine.Rect texR = new(0, 0, width, height);
        tex.filterMode = mode;
        tex.Apply(true);

        RenderTexture rtt = new(width, height, 32);
        Graphics.SetRenderTarget(rtt);
        GL.LoadPixelMatrix(0, 1, 1, 0);
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        Graphics.DrawTexture(new UnityEngine.Rect(0, 0, 1, 1), tex);

        // Update new texture
        tex.Reinitialize(width, height);
        tex.ReadPixels(texR, 0, 0, true);
        tex.Apply(true); //Remove this if you hate us applying textures for you :)
    }

    private void CopySectionTexture(Texture2D src, Texture2D dst, OpenCvSharp.Rect myFace, FilterMode mode = FilterMode.Trilinear)
    {
        UnityEngine.Rect texR = new(0, 0, myFace.Width, myFace.Height);
        src.filterMode = mode;
        src.Apply(true);

        RenderTexture rtt = new(myFace.Width, myFace.Height, 32);
        Graphics.SetRenderTarget(rtt);
        GL.LoadPixelMatrix(0, 1, 1, 0);
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        UnityEngine.Rect section = new((float)myFace.X / src.width, 
                                       ((float)src.height - myFace.Bottom) / src.height,
                                       ((float)myFace.Right - myFace.Left) / src.width,
                                       ((float)myFace.Bottom - myFace.Top) / src.height);
        Graphics.DrawTexture(new UnityEngine.Rect(0, 0, 1, 1), src, section, 0, 0, 0, 0);

        dst.Reinitialize(myFace.Width, myFace.Height);
        dst.ReadPixels(texR, 0, 0, true);
        dst.Apply(true);
    }

    private void CopyTexture(Texture src, Texture2D dst, FilterMode mode = FilterMode.Trilinear)
    {
        UnityEngine.Rect texR = new(0, 0, src.width, src.height);
        src.filterMode = mode;

        RenderTexture rtt = new(src.width, src.height, 32);
        Graphics.SetRenderTarget(rtt);
        GL.LoadPixelMatrix(0, 1, 1, 0);
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        Graphics.DrawTexture(new UnityEngine.Rect(0, 0, 1, 1), src);

        dst.Reinitialize(src.width, src.height);
        dst.ReadPixels(texR, 0, 0, true);
        dst.Apply(true);
    }

    private void RotateImage(Texture2D tex, float angleDegrees)
    {
        int width = tex.width;
        int height = tex.height;
        float halfHeight = height * 0.5f;
        float halfWidth = width * 0.5f;

        var texels = tex.GetRawTextureData<Color32>();
        var copy = System.Buffers.ArrayPool<Color32>.Shared.Rent(texels.Length);
        Unity.Collections.NativeArray<Color32>.Copy(texels, copy, texels.Length);

        float phi = Mathf.Deg2Rad * angleDegrees;
        float cosPhi = Mathf.Cos(phi);
        float sinPhi = Mathf.Sin(phi);

        int address = 0;
        for (int newY = 0; newY < height; newY++)
        {
            for (int newX = 0; newX < width; newX++)
            {
                float cX = newX - halfWidth;
                float cY = newY - halfHeight;
                int oldX = Mathf.RoundToInt(cosPhi * cX + sinPhi * cY + halfWidth);
                int oldY = Mathf.RoundToInt(-sinPhi * cX + cosPhi * cY + halfHeight);
                bool InsideImageBounds = (oldX > -1) & (oldX < width)
                                       & (oldY > -1) & (oldY < height);

                texels[address++] = InsideImageBounds ? copy[oldY * width + oldX] : default;
            }
        }

        // No need to reinitialize or SetPixels - data is already in-place.
        tex.Apply(true);

        System.Buffers.ArrayPool<Color32>.Shared.Return(copy);
    }
}
