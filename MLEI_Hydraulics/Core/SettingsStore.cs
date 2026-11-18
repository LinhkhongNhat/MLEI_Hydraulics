using System;
using System.IO;
using System.Xml.Serialization;

namespace MLEI_Hydraulics
{
    /// <summary>Loads and saves a settings class as XML in %AppData%\MLEI_Hydraulics. Never throws.</summary>
    public static class SettingsStore
    {
        private static string PathFor(string fileName) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MLEI_Hydraulics",
            fileName);

        public static T Load<T>(string fileName) where T : class, new()
        {
            try
            {
                string path = PathFor(fileName);
                if (File.Exists(path))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(T));
                    using (FileStream stream = File.OpenRead(path))
                    {
                        if (serializer.Deserialize(stream) is T loaded)
                            return loaded;
                    }
                }
            }
            catch
            {
                // Missing or corrupted file: fall back to defaults
            }

            return new T();
        }

        public static void Save<T>(string fileName, T settings) where T : class
        {
            try
            {
                string path = PathFor(fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (FileStream stream = File.Create(path))
                {
                    serializer.Serialize(stream, settings);
                }
            }
            catch
            {
                // Remembering is a convenience, never a requirement
            }
        }
    }
}
