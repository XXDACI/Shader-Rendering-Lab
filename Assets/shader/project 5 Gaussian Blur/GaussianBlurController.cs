using UnityEngine;
using UnityEngine.UI;

//[ExecuteAlways]
public class GaussianBlurController : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField]
    private ComputeShader gaussianBlurCompute;

    [Header("Input Picture")]
    [SerializeField]
    private Texture inputTexture;

    [Header("Output")]
    [SerializeField]
    private RawImage outputImage;

    [Header("Offset Settings")]
    [SerializeField]
    private int offset = 4;

    private RenderTexture outputTexture;
    private int gaussianKernel;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (gaussianBlurCompute == null)
        {
            Debug.LogError("没有设置 Gaussian Blur Compute Shader。");
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

        gaussianKernel = gaussianBlurCompute.FindKernel("GaussianBlur");

        CreateOutputTexture();

        int groupX = Mathf.CeilToInt(inputTexture.width / 8.0f);
        int groupY = Mathf.CeilToInt(inputTexture.height / 8.0f);

        gaussianBlurCompute.SetFloat("_Offset", offset);
        gaussianBlurCompute.SetTexture(gaussianKernel, "_InputTexture", inputTexture);
        gaussianBlurCompute.SetTexture(gaussianKernel, "_OutputTexture", outputTexture);
        
        // Start compute shader
        gaussianBlurCompute.Dispatch(gaussianKernel, groupX, groupY, 1);

        outputImage.texture = outputTexture;
    }

    private void CreateOutputTexture()
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
    }
}
