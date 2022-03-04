using System;
using System.IO;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue
{
    [Serializable]
    public struct ByteVec4 : IEquatable<ByteVec4>
    {
        public byte X { get; set; }
        public byte Y { get; set; }
        public byte Z { get; set; }
        public byte W { get; set; }

        public static ByteVec4 Zero { get { return new ByteVec4(0, 0, 0, 0); } }
        public static ByteVec4 Full { get { return new ByteVec4(255, 255, 255, 255); } }

        public ByteVec4(byte x, byte y, byte z, byte w)
        {
            this.X=x;
            this.Y=y;
            this.Z=z;
            this.W=w;
        }

        public ByteVec4 Clone()
        {
            return (ByteVec4)MemberwiseClone();
        }

        public ByteVec4 Add(ByteVec4 vec)
        {
            return new ByteVec4((byte)(X+vec.X), (byte)(Y+vec.Y), (byte)(Z+vec.Z), (byte)(W+vec.W));
        }

        public static ByteVec4 Lerp(ByteVec4 value1, ByteVec4 value2, float amount)
        {
            return new ByteVec4(
                (byte)Lerp(value1.X, value2.X, amount),
                (byte)Lerp(value1.Y, value2.Y, amount),
                (byte)Lerp(value1.Z, value2.Z, amount),
                (byte)Lerp(value1.W, value2.W, amount));
        }

        public static ByteVec4 operator -(ByteVec4 value)
        {
            value.X=(byte)-value.X;
            value.Y=(byte)-value.Y;
            value.W=(byte)-value.W;
            value.Z=(byte)-value.Z;
            return value;
        }

        public static bool operator ==(ByteVec4 value1, ByteVec4 value2)
        {
            return value1.X==value2.X&&value1.Y==value2.Y&&value1.W==value2.W&&value1.Z==value2.Z;
        }

        public static bool operator !=(ByteVec4 value1, ByteVec4 value2)
        {
            return value1.X!=value2.X||value1.Y!=value2.Y||value1.W!=value2.W||value1.Z!=value2.Z;
        }

        public static ByteVec4 operator /(ByteVec4 value1, ByteVec4 value2)
        {
            value1.X/=value2.X;
            value1.Y/=value2.Y;
            value1.Z/=value2.Z;
            value1.W/=value2.W;
            return value1;
        }

        public static ByteVec4 operator +(ByteVec4 value1, ByteVec4 value2)
        {
            value1.X+=value2.X;
            value1.Y+=value2.Y;
            value1.W+=value2.W;
            value1.Z+=value2.Z;
            return value1;
        }

        public static ByteVec4 operator -(ByteVec4 value1, ByteVec4 value2)
        {
            value1.X-=value2.X;
            value1.Y-=value2.Y;
            value1.W-=value2.W;
            value1.Z-=value2.Z;
            return value1;
        }

        public static ByteVec4 operator /(ByteVec4 value1, float divider)
        {
            float factor = 1/divider;
            value1.X=(byte)(value1.X*factor);
            value1.Y=(byte)(value1.Y*factor);
            value1.W=(byte)(value1.W*factor);
            value1.Z=(byte)(value1.Z*factor);
            return value1;
        }

        public static ByteVec4 operator *(ByteVec4 value1, ByteVec4 value2)
        {
            value1.X*=value2.X;
            value1.Y*=value2.Y;
            value1.W*=value2.W;
            value1.Z*=value2.Z;
            return value1;
        }

        public static ByteVec4 operator *(float scaleFactor, ByteVec4 value)
        {
            value.X=(byte)(value.X*scaleFactor);
            value.Y=(byte)(value.Y*scaleFactor);
            value.W=(byte)(value.W*scaleFactor);
            value.Z=(byte)(value.Z*scaleFactor);

            return value;
        }

        public static ByteVec4 operator *(ByteVec4 value, float scaleFactor)
        {
            value.X=(byte)(value.X*scaleFactor);
            value.Y=(byte)(value.Y*scaleFactor);
            value.W=(byte)(value.W*scaleFactor);
            value.Z=(byte)(value.Z*scaleFactor);

            return value;
        }

        public override string ToString()
        {
            return $"[{X},{Y},{Z},{W}]";
        }

        public static double Lerp(double value1, double value2, double amount)
        {
            return value1+(value2-value1)*amount;
        }

        public override bool Equals(object obj)
        {
            return obj is ByteVec4&&Equals((ByteVec4)obj);
        }

        public bool Equals(ByteVec4 other)
        {
            return X==other.X&&
                   Y==other.Y&&
                   Z==other.Z&&
                   W==other.W;
        }

        public override int GetHashCode()
        {
            var hashCode = -1743314642;
            hashCode=hashCode*-1521134295+X.GetHashCode();
            hashCode=hashCode*-1521134295+Y.GetHashCode();
            hashCode=hashCode*-1521134295+Z.GetHashCode();
            hashCode=hashCode*-1521134295+W.GetHashCode();
            return hashCode;
        }
    }
}