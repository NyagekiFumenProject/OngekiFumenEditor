using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.DropActions
{
    public class ConnectableObjectSplitDropAction : IEditorDropHandler
    {
        private readonly ConnectableStartObject startObject;
        private readonly ConnectableStartObject nextStartObject;
        private readonly ConnectableChildObjectBase prevEndObject;
        private readonly Action callback;

        public ConnectableObjectSplitDropAction(ConnectableStartObject startObject, ConnectableChildObjectBase childObject, Action callback = default)
        {
            this.startObject = startObject;
            prevEndObject = CacheLambdaActivator.CreateInstance(childObject.GetType()) as ConnectableChildObjectBase;
            nextStartObject = CacheLambdaActivator.CreateInstance(startObject.GetType()) as ConnectableStartObject;
            this.callback = callback;
        }

        public void Drop(FumenVisualEditorViewModel editor, Point dragEndPoint)
        {
            var dragTGrid = TGridCalculator.ConvertYToTGrid(dragEndPoint.Y, editor);
            var backupStores = new HashSet<ConnectableChildObjectBase>();
            var backupIdxStores = new Dictionary<ConnectableChildObjectBase, int>();
            var affactedObjects = new HashSet<ILaneDockable>();

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("添加物件", () =>
            {
                //计算出需要划分出来的后边子物件
                backupStores.AddRange(startObject.Children.Where(x => x.TGrid > dragTGrid));
                affactedObjects.AddRange(editor.Fumen.Taps.AsEnumerable<ILaneDockable>()
                    .Concat(editor.Fumen.Holds)
                    .Where(x => x.ReferenceLaneStart == startObject));

                //前面删除，后面添加
                foreach (var item in backupStores)
                {
                    startObject.RemoveChildObject(item);
                    backupIdxStores[item] = item.CacheRecoveryChildIndex;
                    item.CacheRecoveryChildIndex = -1;//force add to end
                    nextStartObject.InsertChildObject(item.TGrid, item);
                }

                startObject.AddChildObject(prevEndObject);
                editor.AddObject(nextStartObject);

                editor.OnObjectMovingCanvas(prevEndObject, dragEndPoint);
                editor.OnObjectMovingCanvas(nextStartObject, dragEndPoint);

                foreach (var affactedObj in affactedObjects)
                {
                    var tGrid = affactedObj.TGrid;
                    affactedObj.ReferenceLaneStart = (tGrid >= startObject.MinTGrid && tGrid <= startObject.MaxTGrid ? startObject : nextStartObject) as LaneStartBase;
                }

                editor.Redraw(RedrawTarget.OngekiObjects);
                callback?.Invoke();
            }, () =>
            {
                editor.RemoveObject(nextStartObject);
                startObject.RemoveChildObject(prevEndObject);

                foreach (var item in backupStores)
                {
                    nextStartObject.RemoveChildObject(item);
                    item.CacheRecoveryChildIndex = backupIdxStores[item];
                    startObject.InsertChildObject(item.TGrid, item);
                }

                foreach (var affactedObj in affactedObjects)
                {
                    affactedObj.ReferenceLaneStart = startObject as LaneStartBase;
                }

                backupStores.Clear();
                affactedObjects.Clear();
                editor.Redraw(RedrawTarget.OngekiObjects);
                callback?.Invoke();
            }));
        }
    }
}
