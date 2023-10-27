// From https://github.com/ppy/osu-framework/blob/master/osu.Framework/MathUtils/Interpolation.cs
/*
Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils
{
	public enum EasingTypes
	{
		None,
		Out,
		In,
		InQuad,
		OutQuad,
		InOutQuad,
		InCubic,
		OutCubic,
		InOutCubic,
		InQuart,
		OutQuart,
		InOutQuart,
		InQuint,
		OutQuint,
		InOutQuint,
		InSine,
		OutSine,
		InOutSine,
		InExpo,
		OutExpo,
		InOutExpo,
		InCirc,
		OutCirc,
		InOutCirc,
		InElastic,
		OutElastic,
		OutElasticHalf,
		OutElasticQuarter,
		InOutElastic,
		InBack,
		OutBack,
		InOutBack,
		InBounce,
		OutBounce,
		InOutBounce,
		OutPow10
	}

	public static class Interpolation
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static EasingTypes GetReverseEasing(EasingTypes easing)
		{
			var s = easing.ToString();

			var contain_in = s.Contains("In");
			var contain_out = s.Contains("Out");

			if (contain_in ^ contain_out)
			{
				var x = s;

				if (contain_in)
				{
					x = x.Replace("In", "Out");
				}

				if (contain_out)
				{
					x = x.Replace("Out", "In");
				}

				return Enum.TryParse(x, out EasingTypes r) ? r : EasingTypes.None;
			}

			return easing;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double Lerp(double start, double final, double amount) => start + (final - start) * amount;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ApplyEasing(EasingTypes easing, double from, double to, double cur)
		{
			return from == to ? to : ApplyEasing(easing, (cur - from) / (to - from));
		}

		public static double ApplyEasing(EasingTypes easing, double normalizedValue)
		{
			const double elastic_const = 2 * Math.PI / .3;
			const double elastic_const2 = .3 / 4;

			const double back_const = 1.70158;
			const double back_const2 = back_const * 1.525;

			const double bounce_const = 1 / 2.75;

			switch (easing)
			{
				default:
					return normalizedValue;

				case EasingTypes.In:
				case EasingTypes.InQuad:
					return normalizedValue * normalizedValue;

				case EasingTypes.Out:
				case EasingTypes.OutQuad:
					return normalizedValue * (2 - normalizedValue);

				case EasingTypes.InOutQuad:
					if (normalizedValue < .5) return normalizedValue * normalizedValue * 2;
					return --normalizedValue * normalizedValue * -2 + 1;

				case EasingTypes.InCubic:
					return normalizedValue * normalizedValue * normalizedValue;

				case EasingTypes.OutCubic:
					return --normalizedValue * normalizedValue * normalizedValue + 1;

				case EasingTypes.InOutCubic:
					if (normalizedValue < .5) return normalizedValue * normalizedValue * normalizedValue * 4;
					return --normalizedValue * normalizedValue * normalizedValue * 4 + 1;

				case EasingTypes.InQuart:
					return normalizedValue * normalizedValue * normalizedValue * normalizedValue;

				case EasingTypes.OutQuart:
					return 1 - --normalizedValue * normalizedValue * normalizedValue * normalizedValue;

				case EasingTypes.InOutQuart:
					if (normalizedValue < .5) return normalizedValue * normalizedValue * normalizedValue * normalizedValue * 8;
					return --normalizedValue * normalizedValue * normalizedValue * normalizedValue * -8 + 1;

				case EasingTypes.InQuint:
					return normalizedValue * normalizedValue * normalizedValue * normalizedValue * normalizedValue;

				case EasingTypes.OutQuint:
					return --normalizedValue * normalizedValue * normalizedValue * normalizedValue * normalizedValue + 1;

				case EasingTypes.InOutQuint:
					if (normalizedValue < .5) return normalizedValue * normalizedValue * normalizedValue * normalizedValue * normalizedValue * 16;
					return --normalizedValue * normalizedValue * normalizedValue * normalizedValue * normalizedValue * 16 + 1;

				case EasingTypes.InSine:
					return 1 - Math.Cos(normalizedValue * Math.PI * .5);

				case EasingTypes.OutSine:
					return Math.Sin(normalizedValue * Math.PI * .5);

				case EasingTypes.InOutSine:
					return .5 - .5 * Math.Cos(Math.PI * normalizedValue);

				case EasingTypes.InExpo:
					return Math.Pow(2, 10 * (normalizedValue - 1));

				case EasingTypes.OutExpo:
					return -Math.Pow(2, -10 * normalizedValue) + 1;

				case EasingTypes.InOutExpo:
					if (normalizedValue < .5) return .5 * Math.Pow(2, 20 * normalizedValue - 10);
					return 1 - .5 * Math.Pow(2, -20 * normalizedValue + 10);

				case EasingTypes.InCirc:
					return 1 - Math.Sqrt(1 - normalizedValue * normalizedValue);

				case EasingTypes.OutCirc:
					return Math.Sqrt(1 - --normalizedValue * normalizedValue);

				case EasingTypes.InOutCirc:
					if ((normalizedValue *= 2) < 1) return .5 - .5 * Math.Sqrt(1 - normalizedValue * normalizedValue);
					return .5 * Math.Sqrt(1 - (normalizedValue -= 2) * normalizedValue) + .5;

				case EasingTypes.InElastic:
					return -Math.Pow(2, -10 + 10 * normalizedValue) * Math.Sin((1 - elastic_const2 - normalizedValue) * elastic_const);

				case EasingTypes.OutElastic:
					return Math.Pow(2, -10 * normalizedValue) * Math.Sin((normalizedValue - elastic_const2) * elastic_const) + 1;

				case EasingTypes.OutElasticHalf:
					return Math.Pow(2, -10 * normalizedValue) * Math.Sin((.5 * normalizedValue - elastic_const2) * elastic_const) + 1;

				case EasingTypes.OutElasticQuarter:
					return Math.Pow(2, -10 * normalizedValue) * Math.Sin((.25 * normalizedValue - elastic_const2) * elastic_const) + 1;

				case EasingTypes.InOutElastic:
					if ((normalizedValue *= 2) < 1)
						return -.5 * Math.Pow(2, -10 + 10 * normalizedValue) * Math.Sin((1 - elastic_const2 * 1.5 - normalizedValue) * elastic_const / 1.5);
					return .5 * Math.Pow(2, -10 * --normalizedValue) * Math.Sin((normalizedValue - elastic_const2 * 1.5) * elastic_const / 1.5) + 1;

				case EasingTypes.InBack:
					return normalizedValue * normalizedValue * ((back_const + 1) * normalizedValue - back_const);

				case EasingTypes.OutBack:
					return --normalizedValue * normalizedValue * ((back_const + 1) * normalizedValue + back_const) + 1;

				case EasingTypes.InOutBack:
					if ((normalizedValue *= 2) < 1) return .5 * normalizedValue * normalizedValue * ((back_const2 + 1) * normalizedValue - back_const2);
					return .5 * ((normalizedValue -= 2) * normalizedValue * ((back_const2 + 1) * normalizedValue + back_const2) + 2);

				case EasingTypes.InBounce:
					normalizedValue = 1 - normalizedValue;
					if (normalizedValue < bounce_const)
						return 1 - 7.5625 * normalizedValue * normalizedValue;
					if (normalizedValue < 2 * bounce_const)
						return 1 - (7.5625 * (normalizedValue -= 1.5 * bounce_const) * normalizedValue + .75);
					if (normalizedValue < 2.5 * bounce_const)
						return 1 - (7.5625 * (normalizedValue -= 2.25 * bounce_const) * normalizedValue + .9375);
					return 1 - (7.5625 * (normalizedValue -= 2.625 * bounce_const) * normalizedValue + .984375);

				case EasingTypes.OutBounce:
					if (normalizedValue < bounce_const)
						return 7.5625 * normalizedValue * normalizedValue;
					if (normalizedValue < 2 * bounce_const)
						return 7.5625 * (normalizedValue -= 1.5 * bounce_const) * normalizedValue + .75;
					if (normalizedValue < 2.5 * bounce_const)
						return 7.5625 * (normalizedValue -= 2.25 * bounce_const) * normalizedValue + .9375;
					return 7.5625 * (normalizedValue -= 2.625 * bounce_const) * normalizedValue + .984375;

				case EasingTypes.InOutBounce:
					if (normalizedValue < .5) return .5 - .5 * ApplyEasing(EasingTypes.OutBounce, 1 - normalizedValue * 2);
					return ApplyEasing(EasingTypes.OutBounce, (normalizedValue - .5) * 2) * .5 + .5;

				case EasingTypes.OutPow10:
					return --normalizedValue * Math.Pow(normalizedValue, 10) + 1;
			}
		}
	}
}