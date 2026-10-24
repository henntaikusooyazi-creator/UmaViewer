using System;
using UnityEngine;

namespace Gallop.Live.Rendering
{
    // Explicit producer inputs. The caller qualifies the camera/descriptor,
    // dynamic scale, and temporal-AA policy; no stored MipBias is substituted.
    public static class OriginalPostProcessSamplingMath
    {
        public static Vector4 EvaluateMipBias(float pixelWidth, float targetWidth,
            float dynamicWidthScale, float temporalMipBias)
        {
            Positive(pixelWidth); Positive(targetWidth); Positive(dynamicWidthScale);
            if (float.IsNaN(temporalMipBias) || float.IsInfinity(temporalMipBias))
                throw new ArgumentException("An explicit finite temporal mip bias is required.");
            float scaledWidth = (float)(targetWidth * dynamicWidthScale);
            Positive(scaledWidth);
            float ratio = (float)(pixelWidth / scaledWidth);
            Positive(ratio);
            // Keep the float division and double logarithm/cast boundary.
            float spatial = System.Math.Min((float)-System.Math.Log((double)ratio, 2.0), 0f);
            float temporal = System.Math.Min(temporalMipBias, 0f);
            float bias = System.Math.Min(spatial, temporal);
            return new Vector4(bias, Mathf.Pow(2f, bias), 0f, 0f);
        }
        static void Positive(float value)
        {
            if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Current finite positive widths and dynamic scale are required.");
        }
    }
}
