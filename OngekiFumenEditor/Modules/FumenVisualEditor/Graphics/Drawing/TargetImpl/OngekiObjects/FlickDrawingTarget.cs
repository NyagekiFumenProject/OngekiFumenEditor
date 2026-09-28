using Caliburn.Micro;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class FlickDrawingTarget : CommonBatchDrawTargetBase<Flick>, IDisposable
    {
        public override int DefaultRenderOrder => 1000;

        private IImage texture;
        private IImage exFlickEffTexture;

        private Vector2 leftSize;
        private Vector2 rightSize;
        private Vector2 exTapEffSize;
        private Vector2 selectedEffSize;

        //贴图基础尺寸(来自配置/默认值)，实际尺寸 = 基础尺寸 * 用户设置的贴图缩放系数。
        private Vector2 flickSizeBase;
        private Vector2 exFlickEffSizeBase;

        private List<(Vector2, Vector2, float, Vector4)> exFlickList = new();
        private List<(Vector2, Vector2, float, Vector4)> selectedFlickList = new();
        private List<(Vector2, Vector2, float, Vector4)> normalFlichList = new();

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "FLK", "CFK" };

        public override void Initialize(IRenderManagerImpl impl)
        {
            texture = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\flick.png");
            exFlickEffTexture = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\exflickEffect.png");

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("flick", out var size, out _))
                size = new Vector2(104, 69.333f);
            flickSizeBase = size;

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("exflickEffect", out size, out _))
                size = new Vector2(106, 67f);
            exFlickEffSizeBase = size;

            RebuildTextureSizes();

            Properties.EditorGlobalSetting.Default.PropertyChanged -= EditorGlobalSettingPropertyChanged;
            Properties.EditorGlobalSetting.Default.PropertyChanged += EditorGlobalSettingPropertyChanged;
        }

        private void RebuildTextureSizes()
        {
            var scale = ResourceUtils.TextureSizeScale;
            leftSize = flickSizeBase * scale;
            rightSize = leftSize * new Vector2(-1, 1);
            exTapEffSize = exFlickEffSizeBase * scale;
            selectedEffSize = exTapEffSize * 1.05f;
        }

        private void EditorGlobalSettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Properties.EditorGlobalSetting.TextureSizeScale))
                RebuildTextureSizes();
        }

        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<Flick> objs)
        {
            foreach (var obj in objs)
            {
                var x = XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
                var soflanList = target.Editor._cacheSoflanGroupRecorder.GetCache(obj);
                var y = target.ConvertToViewRelativeY(obj.TGrid, soflanList) + 24;
                var pos = new Vector2((float)x, (float)y);
                var size = obj.Direction == Flick.FlickDirection.Right ? rightSize : leftSize;
                normalFlichList.Add((size, pos, 0f, Vector4.One));

                if (obj.IsCritical)
                {
                    var exTapSize = exTapEffSize;
                    exTapSize.X = Math.Sign(size.X) * exTapSize.X;
                    pos.Y -= 1;

                    exFlickList.Add((exTapSize, pos, 0, Vector4.One));
                }

                if (obj.IsSelected)
                {
                    var selectTapSize = selectedEffSize;
                    selectTapSize.X = Math.Sign(size.X) * selectTapSize.X;
                    pos.Y -= 1;

                    selectedFlickList.Add((selectTapSize, pos, 0, Vector4.One));
                }

                size.X = Math.Abs(size.X);
                target.RegisterSelectableObject(obj, pos, size);
            }

            builder.DrawHighlightBatchTexture(texture, selectedFlickList);
            builder.DrawBatchTexture(texture, normalFlichList);
            builder.DrawBatchTexture(exFlickEffTexture, exFlickList);

            exFlickList.Clear();
            selectedFlickList.Clear();
            normalFlichList.Clear();
        }

        public void Dispose()
        {
            Properties.EditorGlobalSetting.Default.PropertyChanged -= EditorGlobalSettingPropertyChanged;
            texture?.Dispose();
            texture = null;
        }
    }
}
