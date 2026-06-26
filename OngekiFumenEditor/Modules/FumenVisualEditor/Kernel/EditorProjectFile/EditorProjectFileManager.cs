using MigratableSerializer;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Migrations;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System.IO;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile
{
    public class EditorProjectFileManager
    {
        private MigratableSerializerManager manager;

        public EditorProjectFileManager()
        {
            manager = new MigratableSerializerManager();
            void AddSerializer<T>(CommonEditorProjectFileSerializer<T> serializer) where T : EditorProjectDataModelBase
            {
                manager.AddFormatter(serializer);
                manager.AddParser(serializer);
            }

            AddSerializer(new EditorProjectDataModelSerializer_V0_5_2());
            AddSerializer(new EditorProjectDataModelSerializer_Latest());
            manager.AddMigration(new Migration_V0_5_2_To_Latest());
        }

        public Task<EditorProjectDataModel> Create()
        {
            return Task.FromResult(new EditorProjectDataModel());
        }

        public async Task<EditorProjectDataModel> Load(string filePath)
        {
            var buffer = await File.ReadAllBytesAsync(filePath);
            var editorProj = await manager.Load<EditorProjectDataModel>(buffer);

            return editorProj;
        }

        public async Task<EditorProjectDataModel> Clone(EditorProjectDataModel proj)
        {
            // 预估典型项目 ~256 KB,提前分配避免 MemoryStream 内部 byte[] 多次扩容拷贝。
            // MigratableSerializerManager.Load 要求 byte[] 无法传 ArraySegment,
            // 因此 ToArray 必要,但预分配可消除写入阶段 N 次 Array.Resize。
            using var ms = new MemoryStream(256 * 1024);
            await manager.Save(ms, proj, typeof(EditorProjectDataModel));
            return await manager.Load<EditorProjectDataModel>(ms.ToArray());
        }

        public Task Save(string filePath, EditorProjectDataModel proj)
            => Save<EditorProjectDataModel>(filePath, proj);

        public async Task Save<T>(string filePath, EditorProjectDataModel proj) where T : EditorProjectDataModelBase
        {
            using var fs = File.OpenWrite(filePath);
            await manager.Save(fs, proj, typeof(T));
        }
    }
}
