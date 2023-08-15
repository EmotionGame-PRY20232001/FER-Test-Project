using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;

public class WebCamera : MonoBehaviour
{
    [SerializeField]
    private RawImage rawImage;
    [SerializeField]
    private TFLiteTest liteTest;

    private WebCamTexture webCamTexture;
    private WebCamDevice[] devices;
    private CascadeClassifier cascade;
    private OpenCvSharp.Rect myFace;
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
            else
            {
                webCamTexture = new WebCamTexture();
                break;
            }
        }


        rawImage.texture = webCamTexture;
        webCamTexture.Play();

        cascade = new CascadeClassifier(System.IO.Path.Combine(Application.dataPath, "XMLS/haarcascade_frontalface_default.xml"));
    }

    // Update is called once per frame
    void Update()
    {
        Mat frame = OpenCvSharp.Unity.TextureToMat(webCamTexture);
        FindNewFace(frame);
        Display(frame);
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

        Texture newTexture = OpenCvSharp.Unity.MatToTexture(frame);
        rawImage.texture = newTexture;

        if (webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
        {
            //smallTexture = new Texture2D(myFace.Width, myFace.Height);
            //smallTexture.SetPixels(webCamTexture.GetPixels());
            smallTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);
            smallTexture.SetPixels(webCamTexture.GetPixels());
            ChangeToGrayScale(smallTexture);
            Scale(smallTexture, 48, 48);
            liteTest.ChangeSprite(smallTexture);
        }
    }

    //[System.Obsolete]
    public void TakePicture()
    {
        Texture2D texture = new Texture2D(myFace.Width, myFace.Height);
        texture.SetPixels(webCamTexture.GetPixels(myFace.Left, myFace.Top, myFace.Width, myFace.Height));
        ChangeToGrayScale(texture);
        Scale(texture, 48, 48);
        liteTest.ChangeSprite(texture);
        var bytes = texture.EncodeToJPG(100);
        System.IO.File.WriteAllBytes("C:\\Users\\Richard\\Desktop\\foto.jpg", bytes);
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
}
