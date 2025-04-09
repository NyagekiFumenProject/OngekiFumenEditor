using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections.Base.NotQuadTree
{
    public interface IBounded<TX, TY, TData>
        where TX : IDivisionOperators<TX, float, TX>, IAdditionOperators<TX, TX, TX>
        where TY : IDivisionOperators<TY, float, TY>, IAdditionOperators<TY, TY, TY>
    {
        TX X { get; }
        TY Y { get; }
        TX Width { get; }
        TY Height { get; }
        TData Data { get; }

        TX HalfX => X / 2f;
        TY HalfY => Y / 2f;

        TX HalfWidth => Width / 2f;
        TY HalfHeight => Height / 2f;

        TX CenterX => X + HalfWidth;
        TY CenterY => Y + HalfHeight;
    }
}
