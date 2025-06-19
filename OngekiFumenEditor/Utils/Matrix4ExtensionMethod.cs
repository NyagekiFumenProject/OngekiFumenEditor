using OpenTK.Mathematics;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class Matrix4ExtensionMethod
    {
        public static SKMatrix44 ToSkiaMatrix44(this Matrix4 openTkMatrix)
        {
            // 创建Skia矩阵
            var skMatrix = new SKMatrix44();

            // OpenTK是行主序(row-major)，Skia是列主序(column-major)
            // 需要转置矩阵
            skMatrix[0, 0] = openTkMatrix.M11; // Row1.Column1
            skMatrix[0, 1] = openTkMatrix.M12; // Row1.Column2
            skMatrix[0, 2] = openTkMatrix.M13; // Row1.Column3
            skMatrix[0, 3] = openTkMatrix.M14; // Row1.Column4

            skMatrix[1, 0] = openTkMatrix.M21; // Row2.Column1
            skMatrix[1, 1] = openTkMatrix.M22; // Row2.Column2
            skMatrix[1, 2] = openTkMatrix.M23; // Row2.Column3
            skMatrix[1, 3] = openTkMatrix.M24; // Row2.Column4

            skMatrix[2, 0] = openTkMatrix.M31; // Row3.Column1
            skMatrix[2, 1] = openTkMatrix.M32; // Row3.Column2
            skMatrix[2, 2] = openTkMatrix.M33; // Row3.Column3
            skMatrix[2, 3] = openTkMatrix.M34; // Row3.Column4

            skMatrix[3, 0] = openTkMatrix.M41; // Row4.Column1
            skMatrix[3, 1] = openTkMatrix.M42; // Row4.Column2
            skMatrix[3, 2] = openTkMatrix.M43; // Row4.Column3
            skMatrix[3, 3] = openTkMatrix.M44; // Row4.Column4

            return skMatrix;
        }
    }
}
