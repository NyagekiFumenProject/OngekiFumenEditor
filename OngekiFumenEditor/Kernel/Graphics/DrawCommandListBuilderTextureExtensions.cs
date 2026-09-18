using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public static class DrawCommandListBuilderTextureExtensions
    {
        public static void DrawTexture(this IDrawCommandListBuilder builder, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
        {
            builder.DrawTexture(texture, EnumerateTextureInstances(instances));
        }

        public static void DrawBatchTexture(this IDrawCommandListBuilder builder, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
        {
            builder.DrawBatchTexture(texture, EnumerateTextureInstances(instances));
        }

        public static void DrawHighlightBatchTexture(this IDrawCommandListBuilder builder, IImage texture, IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
        {
            builder.DrawHighlightBatchTexture(texture, EnumerateTextureInstances(instances));
        }

        private static IEnumerable<TextureInstance> EnumerateTextureInstances(IEnumerable<(Vector2 size, Vector2 position, float rotation, Vector4 color)> instances)
        {
            foreach (var instance in instances)
                yield return new TextureInstance(instance.size, instance.position, instance.rotation, instance.color);
        }
    }
}
