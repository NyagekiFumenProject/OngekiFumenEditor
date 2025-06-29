﻿using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class TapDrawingTarget : CommonBatchDrawTargetBase<Tap>, IDisposable
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { "TAP", "CTP", "XTP" };

        public override int DefaultRenderOrder => 1200;

        private IImage redTexture;
        private IImage greenTexture;
        private IImage blueTexture;
        private IImage wallTexture;
        private IImage tapExTexture;
        private IImage wallExTexture;
        private IImage untagExTexture;

        private Vector2 tapSize = new Vector2(40, 16);
        private Vector2 exTapEffSize = new Vector2(40, 16);
        private Vector2 leftWallSize = new Vector2(40, 40);
        private Vector2 selectWallTapEffSize = new Vector2(39, 39);
        private Vector2 selectTapEffSize = new Vector2(39, 39);
        private Vector2 exWallTapEffSize = new Vector2(42, 42);

        private Dictionary<IImage, List<(Vector2 size, Vector2 pos, float rotate, Vector4 color)>> normalList = new();
        private Dictionary<IImage, List<(Vector2 size, Vector2 pos, float rotate, Vector4 color)>> exList = new();
        private Dictionary<IImage, List<(Vector2 size, Vector2 pos, float rotate, Vector4 color)>> selectTapList = new();

        private IBatchTextureDrawing batchTextureDrawing;
        private IHighlightBatchTextureDrawing highlightDrawing;

        public override void Initialize(IRenderManagerImpl impl)
        {
            void init(ref IImage texture, string resourceName)
            {
                texture = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\" + resourceName);

                normalList[texture] = new();
                selectTapList[texture] = new();
            }

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("tap", out tapSize, out _))
                tapSize = new Vector2(40, 16);
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("exTapEffect", out exTapEffSize, out _))
                exTapEffSize = new Vector2(70, 30);
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("wall", out leftWallSize, out _))
                leftWallSize = new Vector2(40, 40);
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("selectWallTapEffect", out selectWallTapEffSize, out _))
                selectWallTapEffSize = new Vector2(50, 50);
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("selectTapEffect", out selectTapEffSize, out _))
                selectTapEffSize = tapSize * new Vector2(1.5f, 1.5f);
            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("exWallTapEffect", out exWallTapEffSize, out _))
                exWallTapEffSize = new Vector2(43, 43);

            init(ref redTexture, "redTap.png");
            init(ref greenTexture, "greenTap.png");
            init(ref blueTexture, "blueTap.png");
            init(ref wallTexture, "wallTap.png");
            init(ref tapExTexture, "exTapEffect.png");
            init(ref wallExTexture, "wallTapEffect.png");
            init(ref untagExTexture, "unsetTap.png");

            exList[tapExTexture] = new();
            exList[wallExTexture] = new();

            batchTextureDrawing = impl.BatchTextureDrawing;
            highlightDrawing = impl.HighlightBatchTextureDrawing;
        }

        public void Draw(IFumenEditorDrawingContext target, LaneType? laneType, OngekiMovableObjectBase tap, bool isCritical, SoflanList specifySoflanList = default)
        {
            var texture = laneType switch
            {
                LaneType.Left => redTexture,
                LaneType.Center => greenTexture,
                LaneType.Right => blueTexture,
                LaneType.WallRight or LaneType.WallLeft => wallTexture,
                _ => untagExTexture
            };

            if (texture is null)
                return;

            var size = laneType switch
            {
                LaneType.WallRight => leftWallSize * new Vector2(-1, 1),
                LaneType.WallLeft => leftWallSize,
                _ => tapSize
            };

            var x = XGridCalculator.ConvertXGridToX(tap.XGrid, target.Editor);
            var soflanList = specifySoflanList ?? target.Editor._cacheSoflanGroupRecorder.GetCache(tap);
            var y = target.ConvertToY(tap.TGrid, soflanList);

            var pos = new Vector2((float)x, (float)y);
            normalList[texture].Add((size, pos, 0f, Vector4.One));

            if (tap.IsSelected)
            {
                if (laneType == LaneType.WallLeft || laneType == LaneType.WallRight)
                {
                    size = selectWallTapEffSize * new Vector2(Math.Sign(size.X), 1);
                }
                else
                {
                    size = selectTapEffSize;
                }

                selectTapList[texture].Add((size, pos, 0f, Vector4.One));
            }

            if (isCritical)
            {
                if (laneType == LaneType.WallLeft || laneType == LaneType.WallRight)
                {
                    size = exWallTapEffSize * new Vector2(Math.Sign(size.X), 1);
                    texture = wallExTexture;
                }
                else
                {
                    size = exTapEffSize;
                    texture = tapExTexture;
                }

                exList[texture].Add((size, pos, 0f, Vector4.One));
            }

            size.X = Math.Abs(size.X);
            target.RegisterSelectableObject(tap, pos, size);
        }

        private void ClearList()
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void clear(Dictionary<IImage, List<(Vector2 size, Vector2 pos, float rotate, Vector4 color)>> map)
            {
                foreach (var list in map.Values)
                    list.Clear();
            }

            clear(normalList);
            clear(exList);
            clear(selectTapList);
        }

        public void Dispose()
        {
            redTexture?.Dispose();
            greenTexture?.Dispose();
            blueTexture?.Dispose();
            wallTexture?.Dispose();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<Tap> objs)
        {
            foreach (var tap in objs)
                Draw(target, tap.ReferenceLaneStart?.LaneType, tap, tap.IsCritical);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void draw(Dictionary<IImage, List<(Vector2 size, Vector2 pos, float rotate, Vector4 color)>> map)
            {
                foreach (var item in map)
                    batchTextureDrawing.Draw(target, item.Key, item.Value);
            }

            foreach (var item in selectTapList)
                highlightDrawing.Draw(target, item.Key, item.Value);
            draw(exList);
            draw(normalList);

            ClearList();
        }
    }
}
