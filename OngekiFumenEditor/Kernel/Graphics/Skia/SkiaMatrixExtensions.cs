using SkiaSharp;
using System.Numerics;

namespace OngekiFumenEditor.Kernel.Graphics.Skia
{
    public static class SkiaMatrixExtensions
    {
        public static SKMatrix44 ToSkiaMatrix44(this Matrix4x4 matrix)
        {
            // 创建Skia矩阵
            var skMatrix = new SKMatrix44();

            // System.Numerics.Matrix4x4 是行主序(row-major)，Skia 是列主序(column-major)
            // 需要转置矩阵
            skMatrix[0, 0] = matrix.M11; // Row1.Column1
            skMatrix[0, 1] = matrix.M12; // Row1.Column2
            skMatrix[0, 2] = matrix.M13; // Row1.Column3
            skMatrix[0, 3] = matrix.M14; // Row1.Column4

            skMatrix[1, 0] = matrix.M21; // Row2.Column1
            skMatrix[1, 1] = matrix.M22; // Row2.Column2
            skMatrix[1, 2] = matrix.M23; // Row2.Column3
            skMatrix[1, 3] = matrix.M24; // Row2.Column4

            skMatrix[2, 0] = matrix.M31; // Row3.Column1
            skMatrix[2, 1] = matrix.M32; // Row3.Column2
            skMatrix[2, 2] = matrix.M33; // Row3.Column3
            skMatrix[2, 3] = matrix.M34; // Row3.Column4

            skMatrix[3, 0] = matrix.M41; // Row4.Column1
            skMatrix[3, 1] = matrix.M42; // Row4.Column2
            skMatrix[3, 2] = matrix.M43; // Row4.Column3
            skMatrix[3, 3] = matrix.M44; // Row4.Column4

            return skMatrix;
        }
    }
}
