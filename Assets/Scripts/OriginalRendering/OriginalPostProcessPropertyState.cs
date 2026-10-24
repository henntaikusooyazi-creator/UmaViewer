using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.Live.Rendering
{
    // Explicit, synchronous observation of two separate property sources.
    // The caller qualifies the global binding/default semantics. Unity's getter
    // does not establish presence, a writer, or the original game's owner.
    public sealed class OriginalPostProcessPropertyState
    {
        public readonly Vector4 MaterialVector, GlobalVector;
        public readonly long SelectedSerial, RenderSerial;
        public readonly Camera Camera;
        public readonly Texture Input;
        readonly RenderTexture color, depth;
        readonly Material material;
        readonly Shader shader;
        readonly string materialProperty, globalProperty;
        readonly int width, height;

        OriginalPostProcessPropertyState(OriginalCameraFrame frame, Texture input,
            Material material, string materialProperty, string globalProperty)
        {
            Live(frame, input);
            MaterialVector = ReadMaterial(material, materialProperty, globalProperty);
            GlobalVector = ReadGlobal(globalProperty);
            Camera = frame.Source; Input = input; color = frame.Color; depth = frame.Depth;
            SelectedSerial = frame.SelectedSerial; RenderSerial = frame.RenderSerial;
            width = input.width; height = input.height;
            this.material = material; shader = material.shader;
            this.materialProperty = materialProperty; this.globalProperty = globalProperty;
        }

        public static OriginalPostProcessPropertyState Capture(OriginalCameraFrame frame,
            Texture input, Material material, string materialProperty, string globalProperty)
            => new OriginalPostProcessPropertyState(frame, input, material, materialProperty, globalProperty);

        public void ValidateCurrent(OriginalCameraFrame frame, Texture input)
        {
            Live(frame, input);
            if (frame.Source != Camera || frame.SelectedSerial != SelectedSerial ||
                frame.RenderSerial != RenderSerial || frame.Color != color || frame.Depth != depth ||
                input != Input || input.width != width || input.height != height || !material ||
                material.shader != shader || !ReadMaterial(material, materialProperty, globalProperty).Equals(MaterialVector) ||
                !ReadGlobal(globalProperty).Equals(GlobalVector))
                throw new InvalidOperationException("Post-process properties changed or belong to another draw/update.");
        }

        static Vector4 ReadMaterial(Material source, string property, string global)
        {
            if (!source || !source.shader || string.IsNullOrEmpty(property) || string.IsNullOrEmpty(global))
                throw new InvalidOperationException("A live material and explicit property names are required.");
            int index = source.shader.FindPropertyIndex(property);
            if (index < 0 || (source.shader.GetPropertyType(index) != ShaderPropertyType.Color &&
                source.shader.GetPropertyType(index) != ShaderPropertyType.Vector))
                throw new InvalidOperationException("The material source must declare a color or vector.");
            if (source.shader.FindPropertyIndex(global) >= 0)
                throw new InvalidOperationException("A declared material property cannot be substituted by a global read.");
            return Finite(source.GetVector(property));
        }

        static Vector4 ReadGlobal(string property) => Finite(Shader.GetGlobalVector(property));
        static Vector4 Finite(Vector4 value)
        {
            for (int i = 0; i < 4; i++)
                if (float.IsNaN(value[i]) || float.IsInfinity(value[i]))
                    throw new ArgumentException("Current property vectors must be finite.");
            return value;
        }
        static void Live(OriginalCameraFrame frame, Texture input)
        {
            if (!frame.Source || !frame.Color || !frame.Color.IsCreated() ||
                frame.SelectedSerial <= 0 || frame.RenderSerial <= 0 || !input ||
                input.width <= 0 || input.height <= 0 || (input is RenderTexture rt && !rt.IsCreated()))
                throw new InvalidOperationException("A current rendered frame and live draw input are required.");
        }
    }
}
