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

        public static double Lerp(double start, double final, double amount) => start + (final - start) * amount;

        public static double ApplyEasing(EasingTypes easing, double time)
        {
            const double elastic_const = 2 * Math.PI / .3;
            const double elastic_const2 = .3 / 4;

            const double back_const = 1.70158;
            const double back_const2 = back_const * 1.525;

            const double bounce_const = 1 / 2.75;

            switch (easing)
            {
                default:
                    return time;

                case EasingTypes.In:
                case EasingTypes.InQuad:
                    return time * time;

                case EasingTypes.Out:
                case EasingTypes.OutQuad:
                    return time * (2 - time);

                case EasingTypes.InOutQuad:
                    if (time < .5) return time * time * 2;
                    return --time * time * -2 + 1;

                case EasingTypes.InCubic:
                    return time * time * time;

                case EasingTypes.OutCubic:
                    return --time * time * time + 1;

                case EasingTypes.InOutCubic:
                    if (time < .5) return time * time * time * 4;
                    return --time * time * time * 4 + 1;

                case EasingTypes.InQuart:
                    return time * time * time * time;

                case EasingTypes.OutQuart:
                    return 1 - --time * time * time * time;

                case EasingTypes.InOutQuart:
                    if (time < .5) return time * time * time * time * 8;
                    return --time * time * time * time * -8 + 1;

                case EasingTypes.InQuint:
                    return time * time * time * time * time;

                case EasingTypes.OutQuint:
                    return --time * time * time * time * time + 1;

                case EasingTypes.InOutQuint:
                    if (time < .5) return time * time * time * time * time * 16;
                    return --time * time * time * time * time * 16 + 1;

                case EasingTypes.InSine:
                    return 1 - Math.Cos(time * Math.PI * .5);

                case EasingTypes.OutSine:
                    return Math.Sin(time * Math.PI * .5);

                case EasingTypes.InOutSine:
                    return .5 - .5 * Math.Cos(Math.PI * time);

                case EasingTypes.InExpo:
                    return Math.Pow(2, 10 * (time - 1));

                case EasingTypes.OutExpo:
                    return -Math.Pow(2, -10 * time) + 1;

                case EasingTypes.InOutExpo:
                    if (time < .5) return .5 * Math.Pow(2, 20 * time - 10);
                    return 1 - .5 * Math.Pow(2, -20 * time + 10);

                case EasingTypes.InCirc:
                    return 1 - Math.Sqrt(1 - time * time);

                case EasingTypes.OutCirc:
                    return Math.Sqrt(1 - --time * time);

                case EasingTypes.InOutCirc:
                    if ((time *= 2) < 1) return .5 - .5 * Math.Sqrt(1 - time * time);
                    return .5 * Math.Sqrt(1 - (time -= 2) * time) + .5;

                case EasingTypes.InElastic:
                    return -Math.Pow(2, -10 + 10 * time) * Math.Sin((1 - elastic_const2 - time) * elastic_const);

                case EasingTypes.OutElastic:
                    return Math.Pow(2, -10 * time) * Math.Sin((time - elastic_const2) * elastic_const) + 1;

                case EasingTypes.OutElasticHalf:
                    return Math.Pow(2, -10 * time) * Math.Sin((.5 * time - elastic_const2) * elastic_const) + 1;

                case EasingTypes.OutElasticQuarter:
                    return Math.Pow(2, -10 * time) * Math.Sin((.25 * time - elastic_const2) * elastic_const) + 1;

                case EasingTypes.InOutElastic:
                    if ((time *= 2) < 1)
                        return -.5 * Math.Pow(2, -10 + 10 * time) * Math.Sin((1 - elastic_const2 * 1.5 - time) * elastic_const / 1.5);
                    return .5 * Math.Pow(2, -10 * --time) * Math.Sin((time - elastic_const2 * 1.5) * elastic_const / 1.5) + 1;

                case EasingTypes.InBack:
                    return time * time * ((back_const + 1) * time - back_const);

                case EasingTypes.OutBack:
                    return --time * time * ((back_const + 1) * time + back_const) + 1;

                case EasingTypes.InOutBack:
                    if ((time *= 2) < 1) return .5 * time * time * ((back_const2 + 1) * time - back_const2);
                    return .5 * ((time -= 2) * time * ((back_const2 + 1) * time + back_const2) + 2);

                case EasingTypes.InBounce:
                    time = 1 - time;
                    if (time < bounce_const)
                        return 1 - 7.5625 * time * time;
                    if (time < 2 * bounce_const)
                        return 1 - (7.5625 * (time -= 1.5 * bounce_const) * time + .75);
                    if (time < 2.5 * bounce_const)
                        return 1 - (7.5625 * (time -= 2.25 * bounce_const) * time + .9375);
                    return 1 - (7.5625 * (time -= 2.625 * bounce_const) * time + .984375);

                case EasingTypes.OutBounce:
                    if (time < bounce_const)
                        return 7.5625 * time * time;
                    if (time < 2 * bounce_const)
                        return 7.5625 * (time -= 1.5 * bounce_const) * time + .75;
                    if (time < 2.5 * bounce_const)
                        return 7.5625 * (time -= 2.25 * bounce_const) * time + .9375;
                    return 7.5625 * (time -= 2.625 * bounce_const) * time + .984375;

                case EasingTypes.InOutBounce:
                    if (time < .5) return .5 - .5 * ApplyEasing(EasingTypes.OutBounce, 1 - time * 2);
                    return ApplyEasing(EasingTypes.OutBounce, (time - .5) * 2) * .5 + .5;

                case EasingTypes.OutPow10:
                    return --time * Math.Pow(time, 10) + 1;
            }
        }
    }
}