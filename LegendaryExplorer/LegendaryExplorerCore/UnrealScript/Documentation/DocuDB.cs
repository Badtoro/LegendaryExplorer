using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.UnrealScript.Documentation
{
    /// <summary>
    /// Root object for documentation of UnrealScript class objects.
    /// </summary>
    public class DocuDB
    {
        /// <summary>
        /// The loaded documentation databases.
        /// </summary>
        private static Dictionary<MEGame, DocuDB> LoadedDBs { get; } = new();

        /// <summary>
        /// Contains a mapping of class names to their documentation objects.
        /// </summary>
        [JsonProperty("classes")]
        public CaseInsensitiveDictionary<DocuClassEntry> ClassDocumentation { get; set; } = new();

        /// <summary>
        /// The time at which this documentation database was last updated.
        /// </summary>
        [JsonProperty("last_updated")]
        public DateTime UpdateTime { get; set; }

        public DocuDB() { }

        /// <summary>
        /// Fetches a documentation database, loading it if it isn't already loaded.
        /// </summary>
        /// <param name="game">Game to fetch</param>
        /// <returns></returns>
        public static DocuDB LoadDocuDB(MEGame game, string filePath)
        {
            if (LoadedDBs.TryGetValue(game, out var loaded))
            {
                return loaded;
            }

            if (File.Exists(filePath))
            {
                try
                {
                    loaded = JsonConvert.DeserializeObject<DocuDB>(File.ReadAllText(filePath));
                    LoadedDBs[game] = loaded;
                }
                catch (Exception ex)
                {
                    // Failed to load document DB.
                    return null;
                }
            }

            return loaded;
        }

#if DEBUG

        /// <summary>
        /// Generates a blank databse based on the unrealobjectinfo db.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static DocuDB GetEmptyDB(MEGame game)
        {
            DocuDB db = new DocuDB();
            var oi = GlobalUnrealObjectInfo.GetClasses(game);
            foreach (var cls in oi)
            {
                var newClass = new DocuClassEntry()
                {
                    ClassDocumentation = "No class documentation yet"
                };
                var props = GlobalUnrealObjectInfo.GetAllProperties(game, cls.Key);
                foreach (var member in props)
                {
                    newClass.Members.Add(member.Key.Instanced, new DocuMemberEntry()
                    {
                        MemberClass = member.Value.Reference,
                        MemberDocumentation = "No documentation yet"
                    });
                }

                db.ClassDocumentation[cls.Key] = newClass;
            }

            return db;
        }
#endif
        public string GetDocumentation(IEntry obj)
        {
            var classObj = obj;
            while (classObj is { HasParent: true })
            {
                if (classObj.IsClass)
                    break;

                classObj = classObj.Parent;
            }

            if (!classObj.IsClass)
            {
                // Couldn't find class...
                return null;
            }

            if (ClassDocumentation.TryGetValue(classObj.ObjectName, out var classInfo))
            {
                if (obj == classObj)
                {
                    // It's the class itself.
                    return classInfo.ClassDocumentation;
                }

                // Search members of class
                if (classInfo.Members.TryGetValue(obj.ObjectName.Instanced, out var value))
                {
                    return value.MemberDocumentation;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Describes the objects in a class.
    /// </summary>
    public class DocuClassEntry
    {
        /// <summary>
        /// Link the parent class or function, for further documentation lookups.
        /// </summary>
        [JsonIgnore]
        public DocuClassEntry ParentDocuEntry { get; set; }

        /// <summary>
        /// Members of this class - functions, enums, etc. Anything in the child probe list.
        /// </summary>
        [JsonProperty("members")]
        public CaseInsensitiveDictionary<DocuMemberEntry> Members { get; set; } = new();

        /// <summary>
        /// Contains the text description of this class.
        /// </summary>
        [JsonProperty("documentation")]
        public string ClassDocumentation{get; set;}
    }

    public class DocuMemberEntry
    {
#if DEBUG
        // This is debug only because we don't need this except when debugging.
        [JsonIgnore]
        public string MemberName { get; set; }
#endif

        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }


        /// <summary>
        /// Class of the member
        /// </summary>
        [JsonProperty("class")]
        public string MemberClass { get; set; }

        // Todo: Maybe useful links or something...?
    }
}
