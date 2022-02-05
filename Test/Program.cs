using Caliburn.Micro;
using OngekiFumenEditor;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(new[] { 5, 5, 197, 0, 1, 4, 6, 4, -5, 7, 9, 8, 22, 4, 3, 5 }.MaxMinBy());
        }
    }
}
