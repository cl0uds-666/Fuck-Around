using UnityEngine;

public class SignalVisual : MonoBehaviour
{
    [Header("Colour Blocks")]
    public Renderer[] colourBlockRenderers;

    [Header("Lights")]
    public Light[] signalLights;

    public void SetSignal(Material material, string hexColour)
    {
        if (material != null)
        {
            foreach (Renderer block in colourBlockRenderers)
            {
                if (block != null)
                {
                    block.material = material;
                }
            }
        }

        if (ColorUtility.TryParseHtmlString(hexColour, out Color colour))
        {
            foreach (Light signalLight in signalLights)
            {
                if (signalLight != null)
                {
                    signalLight.color = colour;
                }
            }
        }
    }
}