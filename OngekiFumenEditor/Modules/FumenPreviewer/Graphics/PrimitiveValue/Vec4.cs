namespace ReOsuStoryboardPlayer.Core.PrimitiveValue
{
    public struct Vec4
    {
        public static Vec4 zero = new Vec4();

        private float __x;
        private float __y;
        private float __z;
        private float __w;
        public float x { get { return __x; } set { __x=value; } }
        public float y { get { return __y; } set { __y=value; } }
        public float z { get { return __z; } set { __z=value; } }
        public float w { get { return __w; } set { __w=value; } }

        public Vec4(int _x, int _y, int _z)
        {
            __x=_x;
            __y=_y;
            __z=_z;
            __w=1;
        }

        public Vec4(int _x, int _y, int _z, int _w)
        {
            __x=_x;
            __y=_y;
            __z=_z;
            __w=_w;
        }

        public Vec4(float _x, float _y, float _z)
        {
            __x=_x;
            __y=_y;
            __z=_z;
            __w=1;
        }

        public Vec4(float _x, float _y, float _z, float _w)
        {
            __x=_x;
            __y=_y;
            __z=_z;
            __w=_w;
        }

        public Vec4 clone()
        {
            return (Vec4)MemberwiseClone();
        }

        public override string ToString()
        {
            return x+", "+y+", "+z+", "+w;
        }

        public static Vec4 operator -(Vec4 a, Vec4 b)
        {
            return new Vec4(a.x-b.x, a.y-b.y, a.z-b.z, a.w-b.w);
        }

        public static Vec4 operator *(Vec4 a, float b)
        {
            return new Vec4(a.x*b, a.y*b, a.z*b, a.w*b);
        }
    }
}