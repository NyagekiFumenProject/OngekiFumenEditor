using System;
using System.Runtime.CompilerServices;
using GL = OpenTK.Graphics.OpenGL.GL;
using TextureTarget = OpenTK.Graphics.OpenGL.TextureTarget;
using TextureUnit = OpenTK.Graphics.OpenGL.TextureUnit;
using GL4 = OpenTK.Graphics.OpenGL4.GL;
using TextureTarget4 = OpenTK.Graphics.OpenGL4.TextureTarget;
using TextureUnit4 = OpenTK.Graphics.OpenGL4.TextureUnit;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Base
{
    internal static class OpenGLTextureBindingCache
    {
        private const int MaxTrackedTextureUnits = 32;
        private static readonly int[] boundTexture2DByUnit = CreateUnknownBindings();
        private static int activeTextureUnitIndex = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindTexture2D(int textureId, int textureUnitIndex = 0)
        {
            EnsureTextureUnitIndex(textureUnitIndex);
            EnsureActiveTextureUnit(textureUnitIndex);

            if (boundTexture2DByUnit[textureUnitIndex] == textureId)
                return;

            GL.BindTexture(TextureTarget.Texture2D, textureId);
            boundTexture2DByUnit[textureUnitIndex] = textureId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindTexture2D(TextureUnit textureUnit, int textureId)
        {
            BindTexture2D(textureId, ToTextureUnitIndex((int)textureUnit, (int)TextureUnit.Texture0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindTexture2D(TextureUnit4 textureUnit, int textureId)
        {
            var textureUnitIndex = ToTextureUnitIndex((int)textureUnit, (int)TextureUnit4.Texture0);
            EnsureTextureUnitIndex(textureUnitIndex);
            EnsureActiveTextureUnit4(textureUnit, textureUnitIndex);

            if (boundTexture2DByUnit[textureUnitIndex] == textureId)
                return;

            GL4.BindTexture(TextureTarget4.Texture2D, textureId);
            boundTexture2DByUnit[textureUnitIndex] = textureId;
        }

        public static void InvalidateTexture(int textureId)
        {
            for (var i = 0; i < boundTexture2DByUnit.Length; i++)
            {
                if (boundTexture2DByUnit[i] == textureId)
                    boundTexture2DByUnit[i] = int.MinValue;
            }
        }

        public static void Reset()
        {
            Array.Fill(boundTexture2DByUnit, int.MinValue);
            activeTextureUnitIndex = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureActiveTextureUnit(int textureUnitIndex)
        {
            if (activeTextureUnitIndex == textureUnitIndex)
                return;

            GL.ActiveTexture(TextureUnit.Texture0 + textureUnitIndex);
            activeTextureUnitIndex = textureUnitIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureActiveTextureUnit4(TextureUnit4 textureUnit, int textureUnitIndex)
        {
            if (activeTextureUnitIndex == textureUnitIndex)
                return;

            GL4.ActiveTexture(textureUnit);
            activeTextureUnitIndex = textureUnitIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ToTextureUnitIndex(int textureUnit, int texture0)
        {
            return textureUnit - texture0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureTextureUnitIndex(int textureUnitIndex)
        {
            if ((uint)textureUnitIndex >= MaxTrackedTextureUnits)
                throw new ArgumentOutOfRangeException(nameof(textureUnitIndex), textureUnitIndex, $"Only texture units 0..{MaxTrackedTextureUnits - 1} are tracked.");
        }

        private static int[] CreateUnknownBindings()
        {
            var bindings = new int[MaxTrackedTextureUnits];
            Array.Fill(bindings, int.MinValue);
            return bindings;
        }
    }
}
