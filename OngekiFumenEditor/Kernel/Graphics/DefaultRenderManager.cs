using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    [Export(typeof(IRenderManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class DefaultRenderManager : IRenderManager
    {
        private IEnumerable<IRenderManagerImpl> implments;
        private IRenderManagerImpl currentImpl;

        public DefaultRenderManager()
        {
            implments = IoC.GetAll<IRenderManagerImpl>();
            if (implments.GroupBy(x => x.Name).FirstOrDefault(x => x.Count() > 1)?.Key is string conflictName)
                throw new Exception($"There are more render manager objects with same name: {conflictName}");
        }

        public IEnumerable<string> GetAvaliableRenderManagerImplNames()
        {
            return implments.Select(x => x.Name);
        }

        public IRenderManagerImpl GetCurrentRenderManagerImpl()
        {
            if (currentImpl != null)
                return currentImpl;

            var defaultName = Properties.ProgramSetting.Default.DefaultRenderManagerImplementName;
            if (implments.FirstOrDefault(x => x.Name.Equals(defaultName, StringComparison.InvariantCultureIgnoreCase)) is IRenderManagerImpl impl)
                return currentImpl = impl;
            return currentImpl = implments.FirstOrDefault();
        }

        public void SetRenderManagerImpl(string implName)
        {
            Properties.ProgramSetting.Default.DefaultRenderManagerImplementName = implName;
            Properties.ProgramSetting.Default.Save();
        }
    }
}
