using System;
using System.Collections.Generic;
using System.IO;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue
{
    [Serializable]
    public struct HalfVector : IEquatable<HalfVector>
    {
        public Half X;
        public Half Y;

        public static HalfVector Zero { get { return new HalfVector(new Half(0f), new Half(0f)); } }
        public static HalfVector One { get { return new HalfVector(new Half(0f), new Half(0f)); } }

        public HalfVector(Half _x, Half _y)
        {
            X=_x;
            Y=_y;
        }

        public HalfVector(float _x, float _y)
        {
            X=(Half)_x;
            Y=(Half)_y;
        }

        public HalfVector Clone()
        {
            return (HalfVector)MemberwiseClone();
        }

        public HalfVector Add(HalfVector vec)
        {
            return new HalfVector((Half)(X+vec.X), (Half)(Y+vec.Y));
        }

        public static HalfVector Lerp(HalfVector value1, HalfVector value2, Half amount)
        {
            return new HalfVector(
                (Half)Lerp(value1.X, value2.X, amount),
                (Half)Lerp(value1.Y, value2.Y, amount));
        }

        public static HalfVector operator -(HalfVector value)
        {
            value.X=(Half)(-value.X);
            value.Y=(Half)(-value.Y);
            return value;
        }

        public static bool operator ==(HalfVector value1, HalfVector value2)
        {
            return value1.X==value2.X&&value1.Y==value2.Y;
        }

        public static bool operator !=(HalfVector value1, HalfVector value2)
        {
            return value1.X!=value2.X||value1.Y!=value2.Y;
        }

        public static HalfVector operator /(HalfVector value1, HalfVector value2)
        {
            value1.X/=value2.X;
            value1.Y/=value2.Y;
            return value1;
        }

        public static HalfVector operator +(HalfVector value1, HalfVector value2)
        {
            value1.X+=value2.X;
            value1.Y+=value2.Y;
            return value1;
        }

        public static HalfVector operator -(HalfVector value1, HalfVector value2)
        {
            value1.X-=value2.X;
            value1.Y-=value2.Y;
            return value1;
        }

        public static HalfVector operator /(HalfVector value1, Half divider)
        {
            Half factor = (Half)(1.0f/divider);
            value1.X*=factor;
            value1.Y*=factor;
            return value1;
        }

        public static HalfVector operator *(HalfVector value1, HalfVector value2)
        {
            value1.X*=value2.X;
            value1.Y*=value2.Y;
            return value1;
        }

        public static HalfVector operator *(Half scaleFactor, HalfVector value)
        {
            value.X*=scaleFactor;
            value.Y*=scaleFactor;
            return value;
        }

        public static HalfVector operator *(HalfVector value, Half scaleFactor)
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
            return obj is HalfVector&&Equals((HalfVector)obj);
        }

        public bool Equals(HalfVector other)
        {
            return X.Equals(other.X)&&
                   Y.Equals(other.Y)&&
                   X.Equals(other.X)&&
                   Y.Equals(other.Y);
        }

        public override int GetHashCode()
        {
            var hashCode = -810690406;
            hashCode=hashCode*-1521134295+EqualityComparer<Half>.Default.GetHashCode(X);
            hashCode=hashCode*-1521134295+EqualityComparer<Half>.Default.GetHashCode(Y);
            hashCode=hashCode*-1521134295+EqualityComparer<Half>.Default.GetHashCode(X);
            hashCode=hashCode*-1521134295+EqualityComparer<Half>.Default.GetHashCode(Y);
            return hashCode;
        }
    }
}