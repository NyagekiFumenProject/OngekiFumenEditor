using System;
using System.IO;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue
{
    [Serializable]
    public struct Vector : IEquatable<Vector>
    {
        public float X { get; set; }
        public float Y { get; set; }

        public static Vector Zero { get { return new Vector(0, 0); } }
        public static Vector One { get { return new Vector(0, 0); } }

        public Vector(float _x, float _y)
        {
            X=_x;
            Y=_y;
        }

        public Vector Clone()
        {
            return (Vector)MemberwiseClone();
        }

        public Vector Add(Vector vec)
        {
            return new Vector(X+vec.X, Y+vec.Y);
        }

        public static Vector Lerp(Vector value1, Vector value2, float amount)
        {
            return new Vector(
                (float)Lerp(value1.X, value2.X, amount),
                (float)Lerp(value1.Y, value2.Y, amount));
        }

        public static Vector operator -(Vector value)
        {
            value.X=-value.X;
            value.Y=-value.Y;
            return value;
        }

        public static bool operator ==(Vector value1, Vector value2)
        {
            return value1.X==value2.X&&value1.Y==value2.Y;
        }

        public static bool operator !=(Vector value1, Vector value2)
        {
            return value1.X!=value2.X||value1.Y!=value2.Y;
        }

        public static Vector operator /(Vector value1, Vector value2)
        {
            value1.X/=value2.X;
            value1.Y/=value2.Y;
            return value1;
        }

        public static Vector operator +(Vector value1, Vector value2)
        {
            value1.X+=value2.X;
            value1.Y+=value2.Y;
            return value1;
        }

        public static Vector operator -(Vector value1, Vector value2)
        {
            value1.X-=value2.X;
            value1.Y-=value2.Y;
            return value1;
        }

        public static Vector operator /(Vector value1, float divider)
        {
            float factor = 1/divider;
            value1.X*=factor;
            value1.Y*=factor;
            return value1;
        }

        public static Vector operator *(Vector value1, Vector value2)
        {
            value1.X*=value2.X;
            value1.Y*=value2.Y;
            return value1;
        }

        public static Vector operator *(float scaleFactor, Vector value)
        {
            value.X*=scaleFactor;
            value.Y*=scaleFactor;
            return value;
        }

        public static Vector operator *(Vector value, float scaleFactor)
        {
            value.X*=scaleFactor;
            value.Y*=scaleFactor;
            return value;
        }

        public override string ToString()
        {
            return "["+X+";"+Y+"]";
        }

        public static double Lerp(double value1, double value2, double amount)
        {
            return value1+(value2-value1)*amount;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector&&Equals((Vector)obj);
        }

        public bool Equals(Vector other)
        {
            return X==other.X&&
                   Y==other.Y&&
                   X==other.X&&
                   Y==other.Y;
        }

        public override int GetHashCode()
        {
            var hashCode = -810690406;
            hashCode=hashCode*-1521134295+X.GetHashCode();
            hashCode=hashCode*-1521134295+Y.GetHashCode();
            hashCode=hashCode*-1521134295+X.GetHashCode();
            hashCode=hashCode*-1521134295+Y.GetHashCode();
            return hashCode;
        }
    }
}