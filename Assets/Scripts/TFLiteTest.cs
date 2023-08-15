using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TensorFlowLite;
using System.IO;
using TMPro;
using System.Linq;

public class TFLiteTest : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI emocionText;
    [SerializeField]
    Sprite[] emociones;
    [SerializeField]
    SpriteRenderer emotionSpriteRenderer;
    [SerializeField]
    ComputeShader compute;

    [SerializeField]
    float[] input = new float[48*48];
    [SerializeField]
    float[] output = new float[7];

    Sprite emocionSprite;

    private string[] emotions = { "Angry", "Disgust", "Fear", "Happy", "Neutral", "Sad", "Surprise" };
    private string filePath = "Assets/Models/ferModel.tflite";

    private Interpreter interpreter;
    private ComputeBuffer inputBuffer;

    // Start is called before the first frame update
    void Start()
    {
        var modelFile = File.ReadAllBytes(filePath);
        var options = new InterpreterOptions()
        {
            threads = 2,
        };

        emocionText.text = emotions[Random.Range(0, emotions.Length)];
        emocionSprite = emociones[Random.Range(0, emociones.Length)];
        emotionSpriteRenderer.sprite = emocionSprite;

        interpreter = new Interpreter(modelFile, options);
        interpreter.AllocateTensors();

        inputBuffer = new ComputeBuffer(48 * 48, sizeof(float));

        ExecuteModel();
    }

    public void ChangeSprite(Texture2D texture)
    {
        Sprite newSprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 48f);

        emocionSprite = newSprite;
        emotionSpriteRenderer.sprite = emocionSprite;
        ExecuteModel();
    }

    public void ExecuteModel()
    {
        compute.SetTexture(0, "InputTexture", emocionSprite.texture);
        compute.SetBuffer(0, "OutputTensor", inputBuffer);
        compute.Dispatch(0, 48 / 4, 48 / 4, 1);
        inputBuffer.GetData(input);

        float startTime = Time.realtimeSinceStartup;
        interpreter.SetInputTensorData(0, input);
        interpreter.Invoke();
        interpreter.GetOutputTensorData(0, output);
        float finishTime = Time.realtimeSinceStartup;

        float maxValue = output.Max();
        int maxIndex = output.ToList().IndexOf(maxValue);

        Debug.Log(output[0] + ", " + output[1] + ", " + output[2] + ", " + output[3] + ", " + output[4] + ", " + output[5] + ", " + output[6]);
        Debug.Log("Execution time: " + (finishTime- startTime));

        emocionText.text = emotions[maxIndex];
    }

    private void OnDestroy()
    {
        interpreter?.Dispose();
        inputBuffer?.Dispose();
    }
}
