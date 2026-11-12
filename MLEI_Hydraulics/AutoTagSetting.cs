using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;

namespace MLEI_Hydraulics
{
    public enum VerticalTagDirection
    {
        UpRight,
        UpLeft,
        DownRight,
        DownLeft

    }
    public class TagTypeRef
    {
        public string FamilyName { get; set; }
        public string TypeName { get; set; }

        public TagTypeRef() { }
        public TagTypeRef(string familyName, string typeName)
        {
            FamilyName = familyName;
            TypeName = typeName;
        }

        public override string ToString() => $"{FamilyName} : {TypeName}";
    }

    public class AutoTagSettings
    {
        public TagTypeRef Horizontal { get; set; } = new TagTypeRef("AN_PIPE ANNOTATIONS", "SIZE");
        public TagTypeRef Sloped { get; set; } = new TagTypeRef("AN_PIPE ANNOTATIONS", "SIZE_GRADE");
        public TagTypeRef Vertical { get; set; } = new TagTypeRef("AN_HEXTAG", "COMMENT DOWN");

        // %AppData%\MLEI_Hydraulics\AutoTagSettings.xml
        private static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MLEI_Hydraulics");
                return Path.Combine(dir, "AutoTagSettings.xml");
            }
        }

        /// <summary>Loads the last saved choices, or the defaults if there is no usable file.</summary>
        public static AutoTagSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AutoTagSettings));
                    using (FileStream stream = File.OpenRead(FilePath))
                    {
                        AutoTagSettings loaded = serializer.Deserialize(stream) as AutoTagSettings;
                        if (loaded != null &&
                            loaded.Horizontal != null &&
                            loaded.Sloped != null &&
                            loaded.Vertical != null)
                        {
                            return loaded;
                        }
                    }
                }
            }
            catch
            {
                // Missing, locked or corrupted file: fall back to the defaults
            }

            return new AutoTagSettings();
        }

        /// <summary>Saves the choices. Never throws: remembering is a convenience, not a requirement.</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                XmlSerializer serializer = new XmlSerializer(typeof(AutoTagSettings));
                using (FileStream stream = File.Create(FilePath))
                {
                    serializer.Serialize(stream, this);
                }
            }
            catch
            {
                // Ignore: tagging should still go ahead if the file can't be written
            }
        }
    }
}
