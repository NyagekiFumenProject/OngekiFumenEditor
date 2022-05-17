using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public class EditorProjectDataUtils
    {
        private static JsonSerializerOptions JsonSerializerOptions;

        private class TimeSpanJsonConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var result = default(TimeSpan);
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            break;
                        if (reader.GetString() == "Ticks")
                        {
                            if (!reader.Read())
                                throw new Exception("Json parse TimeSpan rrror");
                            var ticks = reader.GetInt64();
                            result =  TimeSpan.FromTicks(ticks);
                        }
                    }
                }
                return result;
            }

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("Ticks", value.Ticks);
                writer.WriteEndObject();
            }
        }

        static EditorProjectDataUtils()
        {
            JsonSerializerOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

            JsonSerializerOptions.Converters.Add(new TimeSpanJsonConverter());
        }

        public static string GetRelativeOngekiFumenFilePath(string editorProjectFilePath) => Path.Combine(Path.GetDirectoryName(editorProjectFilePath), Path.GetFileNameWithoutExtension(editorProjectFilePath) + ".ogkr");

        public static async Task<EditorProjectDataModel> TryLoadFromFileAsync(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            var projectData = await JsonSerializer.DeserializeAsync<EditorProjectDataModel>(fileStream, JsonSerializerOptions);

            var fumenFilePath = projectData.FumenFilePath ?? GetRelativeOngekiFumenFilePath(filePath);
            if (projectData.FumenFilePath is null)
                projectData.FumenFilePath = fumenFilePath;
            using var fumenFileStream = File.OpenRead(fumenFilePath);
            var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(fumenFilePath);
            if (fumenDeserializer is null)
                throw new NotSupportedException($"不支持此谱面文件的解析:{fumenFilePath}");
            var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);
            projectData.Fumen = fumen;

            ApplyBulletPalleteListEditorData(projectData);

            return projectData;
        }

        private static void ApplyBulletPalleteListEditorData(EditorProjectDataModel projectData)
        {
            foreach (var bpl in projectData.Fumen.BulletPalleteList)
            {
                if (projectData.StoreBulletPalleteEditorDatas.TryGetValue(bpl.StrID, out var storeEditorData))
                {
                    bpl.EditorName = storeEditorData.Name;
                    bpl.EditorAxuiliaryLineColor = storeEditorData.AuxiliaryLineColor;
                }
            }
        }

        private static void StoreBulletPalleteListEditorData(EditorProjectDataModel projectData)
        {
            foreach (var bpl in projectData.Fumen.BulletPalleteList)
            {
                if (projectData.StoreBulletPalleteEditorDatas.TryGetValue(bpl.StrID, out var storeEditorData))
                {
                    storeEditorData.Name = bpl.EditorName;
                    storeEditorData.AuxiliaryLineColor = bpl.EditorAxuiliaryLineColor;
                }
                else
                {
                    projectData.StoreBulletPalleteEditorDatas[bpl.StrID] = new()
                    {
                        AuxiliaryLineColor = bpl.EditorAxuiliaryLineColor,
                        Name = bpl.EditorName
                    };
                }
            }
        }

        public static async Task TrySaveToFileAsync(string filePath, EditorProjectDataModel editorProject)
        {
            try
            {
                using var fileStream = File.Open(filePath, FileMode.Create);
                StoreBulletPalleteListEditorData(editorProject);

                var fumenFilePath = editorProject.FumenFilePath ?? GetRelativeOngekiFumenFilePath(filePath);
                if (editorProject.FumenFilePath is null)
                    editorProject.FumenFilePath = fumenFilePath;

                var serializer = IoC.Get<IFumenParserManager>().GetSerializer(fumenFilePath);
                if (serializer is null)
                    throw new NotSupportedException($"不支持保存此文件格式:{fumenFilePath}");

                await JsonSerializer.SerializeAsync(fileStream, editorProject, JsonSerializerOptions);
                await File.WriteAllBytesAsync(fumenFilePath, await serializer.SerializeAsync(editorProject.Fumen));
            }
            catch (Exception e)
            {
                var msg = $"无法保存:{e.Message}{Environment.NewLine}{e.StackTrace}";
                Log.LogError(msg);
                MessageBox.Show(msg);
            }
        }
    }
}
