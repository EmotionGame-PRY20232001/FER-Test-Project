using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;

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

        rawImage.texture = webCamTexture;
        webCamTexture.Play();

        FileStorage storageFaces = new FileStorage(haarCascasde.text, FileStorage.Mode.Read | FileStorage.Mode.Memory);
        cascade = new CascadeClassifier();
        if (!cascade.Read(storageFaces.GetFirstTopLevelNode()))
            throw new System.Exception("FaceProcessor.Initialize: Failed to load faces cascade classifier");
    }

    // Update is called once per frame
    void Update()
    {
        finalTexture = new Texture2D(webCamTexture.width, webCamTexture.height);
        finalTexture.SetPixels(webCamTexture.GetPixels());
#if UNITY_ANDROID && !UNITY_EDITOR
        RotateImage(finalTexture, 90);
#endif

        Mat webImage = OpenCvSharp.Unity.TextureToMat(finalTexture);
        FindNewFace(webImage);
        Display(webImage);
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
                smallTexture.SetPixels(finalTexture.GetPixels(myFace.X, finalTexture.height - myFace.Bottom, myFace.Width, myFace.Height));
            }
            catch
            {
                return;
            }
            
            Scale(smallTexture, 48, 48);
            ChangeToGrayScale(smallTexture);
            liteTest.ChangeSprite(smallTexture);
        }

        Texture newTexture = OpenCvSharp.Unity.MatToTexture(frame);
        rawImage.texture = newTexture;
    }

    private void ChangeToGrayScale(Texture2D texture)
    {
        var texColors = texture.GetPixels();
        for (int i = 0; i < texColors.Length; i++)
        {
            var grayValue = Vector3.Dot(new Vector3(texColors[i].r, texColors[i].g, texColors[i].b), new Vector3(0.3f, 0.59f, 0.11f));
            texColors[i] = new Color(grayValue, grayValue, grayValue, 1);
        }
        texture.SetPixels(texColors);
        texture.Apply();
    }

    private Texture2D Scaled(Texture2D src, int width, int height, FilterMode mode = FilterMode.Trilinear)
    {
        UnityEngine.Rect texR = new(0, 0, width, height);
        _gpu_scale(src, width, height, mode);

        //Get rendered data back to a new texture
        Texture2D result = new(width, height, TextureFormat.ARGB32, true);
        result.Reinitialize(width, height);
        result.ReadPixels(texR, 0, 0, true);
        return result;
    }

    private void Scale(Texture2D tex, int width, int height, FilterMode mode = FilterMode.Trilinear)
    {
        UnityEngine.Rect texR = new(0, 0, width, height);
        _gpu_scale(tex, width, height, mode);

        // Update new texture
        tex.Reinitialize(width, height);
        tex.ReadPixels(texR, 0, 0, true);
        tex.Apply(true); //Remove this if you hate us applying textures for you :)
    }

    private static void _gpu_scale(Texture2D src, int width, int height, FilterMode fmode)
    {
        //We need the source texture in VRAM because we render with it
        src.filterMode = fmode;
        src.Apply(true);

        //Using RTT for best quality and performance. Thanks, Unity 5
        RenderTexture rtt = new(width, height, 32);

        //Set the RTT in order to render to it
        Graphics.SetRenderTarget(rtt);

        //Setup 2D matrix in range 0..1, so nobody needs to care about sized
        GL.LoadPixelMatrix(0, 1, 1, 0);

        //Then clear & draw the texture to fill the entire RTT.
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        Graphics.DrawTexture(new UnityEngine.Rect(0, 0, 1, 1), src);
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
