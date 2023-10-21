using UnityEngine;
using TensorFlowLite;
using TMPro;
using System.Linq;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class TFLiteTest : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI emocionText;
    [SerializeField]
    private TextMeshProUGUI averageText;
    [SerializeField]
    private RawImage faceImage;
    [SerializeField]
    private Material grayMaterial;
    [SerializeField]
    private ComputeShader compute;
    [SerializeField, FilePopup("*.tflite")]
    private string filePath = "ferModel.tflite";

    private Interpreter interpreter;
    private ComputeBuffer inputBuffer;
    private float[] input = new float[48*48];
    private float[] output = new float[7];
    private byte[] modelFile;
    private List<float> processTimes = new List<float>();
    private string[] emotions = { "Enojo", "Disgusto", "Miedo", "Feliz", "Neutral", "Triste", "Sorpresa" };

    private void Awake()
    {
        faceImage.material = grayMaterial;
        modelFile = FileUtil.LoadFile(filePath);

        var options = new InterpreterOptions()
        {
            threads = 2,
        };
        interpreter = new Interpreter(modelFile, options);
        interpreter.AllocateTensors();
        inputBuffer = new ComputeBuffer(48 * 48, sizeof(float));
    }

    public void ChangeSprite(Texture2D texture)
    {
        faceImage.texture = texture;
        ExecuteModel();
    }

    public void ExecuteModel()
    {
        compute.SetTexture(0, "InputTexture", faceImage.texture);
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

        if (processTimes.Count < 75)
        {
            processTimes.Add(finishTime-startTime);
            averageText.text = $"Media: {(processTimes.Average() * 1000).ToString("F2")}ms";
        }

        Debug.Log($"Execution time: {(finishTime - startTime) * 1000}ms");

        emocionText.text = emotions[maxIndex];
    }

    private void OnDestroy()
    {
        interpreter?.Dispose();
        inputBuffer?.Dispose();
    }
}
