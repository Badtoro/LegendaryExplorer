using System.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Documentation;

namespace LegendaryExplorer.Misc
{
    public static class LEXDocuDB
    {
        /// <summary>
        /// LEX convenience wrapper for loading/fetching DB
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static DocuDB LoadDocuDB(MEGame game)
        {
            // Maybe we should cache db object here to avoid call to AppDirectories.DocusDB which calls .CreateDirectory()
            return DocuDB.LoadDocuDB(game, Path.Combine(AppDirectories.DocuDBsFolder, game.ToString()));
        }

        public static string GetDocumentationForClassMember(MEGame game, string className, NameReference propertyName)
        {
            var db = LoadDocuDB(game);
            if (db.ClassDocumentation.TryGetValue(className, out var cls) && cls.Variables.TryGetValue(propertyName.Instanced, out var member))
            {
                return member.MemberDocumentation;
            }

            return null;
        }
    }
}
