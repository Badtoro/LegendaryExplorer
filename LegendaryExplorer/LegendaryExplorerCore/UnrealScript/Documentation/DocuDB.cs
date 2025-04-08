using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
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
        /// Generates a blank database based on classes found in the game.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static DocuDB GetEmptyDB(MEGame game)
        {
            DocuDB db = new DocuDB();

            // Load cache
            TieredPackageCache tpc = TieredPackageCache.GetGlobalPackageCache(game, gameRootPath: ME3TweaksBackups.GetGameBackupPath(game));

            var toBreak = false;
            foreach (var f in MELoadedFiles.GetFilesLoadedInGame(game, gameRootOverride: ME3TweaksBackups.GetGameBackupPath(game)))
            {
                var quickPackage = MEPackageHandler.UnsafePartialLoad(f.Value, x => false);
                var hasUndocedClasses = quickPackage.Exports.Any(x => x.IsClass && !db.ClassDocumentation.ContainsKey(x.ObjectName));
                if (!hasUndocedClasses)
                    continue;

                using var package = MEPackageHandler.OpenMEPackage(f.Value);
                FileLib fl = null;
                UnrealScriptOptionsPackage usop = null;
                foreach (var cls in package.Exports.Where(x => x.IsClass))
                {
                    if (db.ClassDocumentation.ContainsKey(cls.ObjectName))
                        continue;

                    if (fl == null)
                    {
                        fl = new FileLib(package);
                        var cache = tpc.ChainNewCache();
                        usop = new UnrealScriptOptionsPackage() { Cache = cache };
                        fl.Initialize(usop);
                        if (fl.HadInitializationError)
                            break; // Can't do this file.
                    }

                    foreach (var fx in fl.ReadonlySymbolTable.Types.Where(x => x.Type == ASTNodeType.Class))
                    {
                        if (db.ClassDocumentation.ContainsKey(fx.Name))
                            continue;

                        Debug.WriteLine($"{fx.Name}: {fx.Type}");
                        var classD = GenerateDocuClass(fx);
                        db.ClassDocumentation[fx.Name] = classD;
                    }

                    toBreak = true;
                }

                if (toBreak)
                    break;
            }

            return db;
        }

        private static DocuClassEntry GenerateDocuClass(VariableType classDef)
        {
            DocuClassEntry dce = new DocuClassEntry();
            dce.Variables = new CaseInsensitiveDictionary<DocuMemberEntry>();
            dce.Functions = new CaseInsensitiveDictionary<DocuFunctionEntry>();
            dce.Types = new CaseInsensitiveDictionary<DocuTypeEntry>();
            dce.States = new CaseInsensitiveDictionary<DocuStateEntry>();


            if (classDef is Class cls)
            {
                // Variables
                foreach (var v in cls.VariableDeclarations)
                {
                    dce.Variables.Add(v.Name, new DocuMemberEntry() { MemberClass = v.VarType.Name });
                }


                // Functions
                foreach (var v in cls.Functions)
                {
                    var func = new DocuFunctionEntry();
                    func.FunctionMembers = new();
                    foreach (var param in v.Parameters)
                    {
                        func.FunctionMembers.Add(param.Name, new DocuMemberEntry() { MemberClass = param.VarType.Name });
                    }
                    dce.Functions.Add(v.Name, func);
                }

                // Structs, Consts, Enums
                foreach (var type in cls.TypeDeclarations)
                {
                    var dType = new DocuTypeEntry();

                    if (type is Struct tStruct)
                    {
                        dType.Members = new();
                        foreach (var eValue in tStruct.VariableDeclarations)
                        {
                            dType.Members[eValue.Name] = new DocuMemberEntry();
                        }

                        // Type declarations?
                    }
                    else if (type is Const tConst)
                    {
                        // Nothing here
                    }
                    else if (type is Enumeration tEnum)
                    {
                        dType.Members = new();
                        foreach (var eValue in tEnum.Values)
                        {
                            dType.Members[eValue.Name] = new DocuMemberEntry();
                        }
                    }
                    dce.Types[type.Name] = dType;

                }

                // States
                foreach (var state in cls.States)
                {
                    var dState = new DocuStateEntry();
                    dState.Functions = new();
                    foreach (var v in state.Functions)
                    {
                        var func = new DocuFunctionEntry();
                        func.FunctionMembers = new();
                        foreach (var param in v.Parameters)
                        {
                            func.FunctionMembers.Add(param.Name, new DocuMemberEntry() { MemberClass = param.VarType.Name });
                        }
                        dState.Functions.Add(v.Name, func);
                    }
                    dce.States.Add(state.Name, dState);
                }
            }
            return dce;
        }
#endif
        public string GetDocumentation(string className, string memberName, string subMemberName = null)
        {
            if (ClassDocumentation.TryGetValue(className, out var cls))
            {
                // Member
                if (cls.Variables.TryGetValue(memberName, out var member))
                {
                    return member.MemberDocumentation;
                }

                // Function
                if (cls.Functions.TryGetValue(memberName, out var function))
                {
                    // Are we getting a parameter on the func?
                    if (subMemberName != null)
                    {
                        function.FunctionMembers.TryGetValue(subMemberName, out var subMember);
                        return subMember?.MemberDocumentation;
                    }

                    return function.MemberDocumentation;
                }

                // Enum, Structs, Consts
                if (cls.Types.TryGetValue(memberName, out var type))
                {


                }


                // States
                if (cls.Functions.TryGetValue(memberName, out var state))
                {
                    //    if (subMemberName != null)
                    //    {
                    //        function.FunctionMembers.TryGetValue(subMemberName, out var subMember);
                    //        return subMember?.MemberDocumentation;
                    //    }

                    //    return function.MemberDocumentation;
                }
            }

            return null;
        }

        public string GetDocumentation(IEntry obj)
        {
            // This is really poor and definitely needs improved

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
        [JsonProperty("parent")]
        public string ParentClass { get; set; }

        /// <summary>
        /// Variable members of this class
        /// </summary>
        [JsonProperty("variables")]
        public CaseInsensitiveDictionary<DocuMemberEntry> Variables { get; set; }

        /// <summary>
        /// Function members of this class
        /// </summary>
        [JsonProperty("functions")]
        public CaseInsensitiveDictionary<DocuFunctionEntry> Functions { get; set; }

        /// <summary>
        /// Function members of this class
        /// </summary>
        [JsonProperty("types")]
        public CaseInsensitiveDictionary<DocuTypeEntry> Types { get; set; }

        /// <summary>
        /// Function members of this class
        /// </summary>
        [JsonProperty("states")]
        public CaseInsensitiveDictionary<DocuStateEntry> States { get; set; }

        /// <summary>
        /// Contains the text description of this class.
        /// </summary>
        [JsonProperty("documentation")]
        public string ClassDocumentation { get; set; }
    }

    public class DocuStateEntry
    {
        /// <summary>
        /// Function members of this class
        /// </summary>
        [JsonProperty("functions")]
        public CaseInsensitiveDictionary<DocuFunctionEntry> Functions { get; set; }

        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }
    }

    public class DocuTypeEntry
    {
        // Structs, Enums
        public CaseInsensitiveDictionary<DocuMemberEntry> Members { get; set; } = new();

        // Const doesn't have any members
        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }
    }

    public class DocuFunctionEntry
    {
#if DEBUG
        // This is debug only because we don't need this except when debugging.
        [JsonIgnore]
        public string FunctionName { get; set; }
#endif

        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }


        /// <summary>
        /// Class of the member
        /// </summary>
        [JsonProperty("signature_members")]
        public CaseInsensitiveDictionary<DocuMemberEntry> FunctionMembers { get; set; }
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
