using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Shapes;

namespace OngekiFumenEditor.Utils.Settings
{
    public class OverlayJsonSettingsProvider : SettingsProvider
    {
        public override string ApplicationName
        {
            get => AppDomain.CurrentDomain.FriendlyName;
            set { }
        }

        public override string Name => nameof(OverlayJsonSettingsProvider);

        public static OverlayJsonSettingsProvider Default { get; }
            = new OverlayJsonSettingsProvider(
                AppDirectoryHelper.ResolveRelative("config.json"));

        private readonly string jsonFile;

        private object syncRoot = new();

        private JsonObject jsonRoot = new JsonObject();

        private OverlayJsonSettingsProvider(string jsonFile)
        {
            this.jsonFile = jsonFile;
            Load();
        }

        private void Load()
        {
            lock (syncRoot)
            {
                try
                {
                    var json = File.ReadAllText(jsonFile, Encoding.UTF8);
                    jsonRoot = JsonNode.Parse(json) as JsonObject ?? [];
                }
                catch (Exception e)
                {
                    Log.LogWarn($"Load overlay json settings failed: {jsonFile}\n{e}");
                }
            }
        }

        private void Save()
        {
            lock (syncRoot)
            {
                try
                {
                    var json = JsonSerializer.Serialize(
                        jsonRoot
                        ,
                        CommonJsonSerializerOptions.Default);

                    File.WriteAllText(jsonFile, json, Encoding.UTF8);
                }
                catch (Exception e)
                {
                    Log.LogError($"Save overlay json settings failed: {jsonFile}", e);
                }
            }
        }

        public override SettingsPropertyValueCollection GetPropertyValues(
            SettingsContext context,
            SettingsPropertyCollection collection)
        {
            var result = new SettingsPropertyValueCollection();
            var groupName = context["GroupName"]?.ToString();
            var groupNode = GetChildJsonNodeFromPath(groupName);

            foreach (SettingsProperty property in collection)
            {
                var value = new SettingsPropertyValue(property);

                if (!groupNode.TryGetPropertyValue(property.Name, out var jsonObj))
                {
                    value.PropertyValue = TypeConvertHelper.ConvertFromString(property.PropertyType, property.DefaultValue?.ToString());
                }
                else
                {
                    try
                    {
                        value.PropertyValue = JsonSerializer.Deserialize(jsonObj.ToJsonString(), property.PropertyType, CommonJsonSerializerOptions.Default);
                    }
                    catch (Exception e)
                    {
                        Log.LogWarn($"Deserialize setting property failed: group={groupName}, property={property.Name}\n{e}");
                        value.PropertyValue = TypeConvertHelper.ConvertFromString(property.PropertyType, property.DefaultValue?.ToString());
                    }
                }

                result.Add(value);
            }

            return result;
        }

        public override void SetPropertyValues(
            SettingsContext context,
            SettingsPropertyValueCollection collection)
        {
            lock (syncRoot)
            {
                var groupName = context["GroupName"]?.ToString();
                var groupNode = GetChildJsonNodeFromPath(groupName);

                foreach (SettingsPropertyValue value in collection)
                {
                    try
                    {
                        groupNode[value.Name] = JsonSerializer.SerializeToNode(value.PropertyValue, CommonJsonSerializerOptions.Default);
                    }
                    catch
                    {
                        // 忽略单项失败
                    }
                }
            }

            Save();
        }

        private JsonObject GetChildJsonNodeFromPath(string path)
        {
            var current = jsonRoot;

            foreach (string part in path?.Split('.') ?? [])
            {
                if (current is null)
                {
                    throw new InvalidOperationException(
                        $"Node is not JsonObject: {part}");
                }

                if (!current.TryGetPropertyValue(part, out var child)
                    || child == null)
                {
                    child = new JsonObject();
                    current[part] = child;
                }

                current = child as JsonObject;
            }

            return current;
        }
    }
}
