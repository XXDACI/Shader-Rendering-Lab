using UnityEngine;
using UnityEngine.UI;

//[ExecuteAlways]
public class KawaseBlurController : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField]
    private ComputeShader kawaseBlurCompute;

    [Header("Input Picture")]
    [SerializeField]
    private Texture inputTexture;

    [Header("Output")]
    [SerializeField]
    private RawImage outputImage;

    [Header("Blur Settings")]
    [SerializeField]
    private int passCount = 4;

    private RenderTexture outputTexture;
    private RenderTexture temporaryTexture;

    private int kawaseKernel;

    void Start()
    {
        if (kawaseBlurCompute == null)
        {
            Debug.LogError("没有设置 Kawase Blur Compute Shader。");
            return;
        }

        if (inputTexture == null)
        {
            Debug.LogError("没有设置输入图片。");
            return;
        }

        if (outputImage == null)
        {
            Debug.LogError("没有设置输出 RawImage。");
            return;
        }

        kawaseKernel = kawaseBlurCompute.FindKernel("Kawase");

        CreateOutputTextures();

        int groupX = Mathf.CeilToInt(inputTexture.width / 8.0f);
        int groupY = Mathf.CeilToInt(inputTexture.height / 8.0f);

        Texture currentInput = inputTexture;
        RenderTexture currentOutput = outputTexture;

        for (int pass = 0; pass < passCount; pass++)
        {
            float offset = pass + 0.5f;

            kawaseBlurCompute.SetFloat("_Offset", offset);
            kawaseBlurCompute.SetTexture(
                kawaseKernel,
                "_InputTexture",
                currentInput
            );
            kawaseBlurCompute.SetTexture(
                kawaseKernel,
                "_OutputTexture",
                currentOutput
            );

            kawaseBlurCompute.Dispatch(
                kawaseKernel,
                groupX,
                groupY,
                1
            );

            currentInput = currentOutput;

            currentOutput = currentOutput == outputTexture ? temporaryTexture : outputTexture;
        }

        outputImage.texture = currentInput;
    }

    private void CreateOutputTextures()
    {
        outputTexture = new RenderTexture(
            inputTexture.width,
            inputTexture.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear
        );

        outputTexture.enableRandomWrite = true;
        outputTexture.filterMode = FilterMode.Bilinear;
        outputTexture.wrapMode = TextureWrapMode.Clamp;
        outputTexture.Create();

        temporaryTexture = new RenderTexture(
            inputTexture.width,
            inputTexture.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear
        );

        temporaryTexture.enableRandomWrite = true;
        temporaryTexture.filterMode = FilterMode.Bilinear;
        temporaryTexture.wrapMode = TextureWrapMode.Clamp;
        temporaryTexture.Create();
    }

    private void OnDestroy()
    {
        if (outputTexture != null)
        {
            outputTexture.Release();
            if (Application.isPlaying)
                Destroy(outputTexture);
            else
                DestroyImmediate(outputTexture);

            outputTexture = null;
        }

        if (temporaryTexture != null)
        {
            temporaryTexture.Release();
            if (Application.isPlaying)
                Destroy(temporaryTexture);
            else
                DestroyImmediate(temporaryTexture);

            temporaryTexture = null;
        }
    }
}
