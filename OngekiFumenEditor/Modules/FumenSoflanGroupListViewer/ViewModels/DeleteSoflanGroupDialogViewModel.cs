using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Properties;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    public sealed class DeleteSoflanGroupDialogViewModel : Screen
    {
        public DeleteSoflanGroupDialogViewModel(IEnumerable<SoflanGroupWrapItem> groups, int count)
        {
            DisplayName = Resources.SoflanGroupDeleteReferenced;
            Groups = groups.ToArray();
            Replacement = Groups.FirstOrDefault();
            ReferenceMessage = string.Format(Resources.SoflanGroupReferenceCount, count);
        }

        public SoflanGroupWrapItem[] Groups { get; }
        public string ReferenceMessage { get; }

        private SoflanGroupWrapItem replacement;
        public SoflanGroupWrapItem Replacement
        {
            get => replacement;
            set => Set(ref replacement, value);
        }

        public bool DeleteReferences { get; private set; }

        public Task ReplaceAndDelete() => Replacement is null ? Task.CompletedTask : TryCloseAsync(true);

        public Task DeleteAllReferences()
        {
            DeleteReferences = true;
            return TryCloseAsync(true);
        }

        public Task Cancel() => TryCloseAsync(false);
    }
}
