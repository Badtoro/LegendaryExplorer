using System.Text;
using CommandLine;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Documentation;

namespace LegendaryExplorerUnrealDoc
{
    internal class Program
    {
        // For compiling docs.
        static int Main(string[] args)
        {
            bool success = false;
            Parser.Default.ParseArguments<CLIOptions>(args)
                .WithParsed<CLIOptions>(o =>
                {
#if DEBUG
                    // Test only.
                    Console.WriteLine("Generating dbs...");
                    LegendaryExplorerUnrealDoc.debug.Testing.GenerateBlankDocuDBs();
#endif
                    BuildDocs(o);
                    Console.WriteLine("Documentation built.");
                    success = true;
                })
                .WithNotParsed(x =>
                {
                    Console.WriteLine("Invalid arguments specified.");
                });

            if (success)
            {
                return 0;
            }

            return -1;
        }

        static void BuildDocs(CLIOptions options)
        {
            LoadTemplates();

            var inputDirs = Directory.GetDirectories(options.InputFolder);
            var games = new List<MEGame>();
            foreach (var inputDir in inputDirs)
            {
                if (Enum.TryParse<MEGame>(Path.GetFileName(inputDir), out var game))
                {
                    Console.WriteLine($"Detected documentation source for {game}");
                    games.Add(game);
                }
            }

            foreach (var game in games)
            {
                Console.WriteLine($"Generating documentation for {game}");
                var htmlPath = Path.Combine(options.OutputFolder, game.ToString());
                Directory.CreateDirectory(Path.Combine(htmlPath, "classes"));
                Directory.CreateDirectory(Path.Combine(htmlPath, "enums"));
                Directory.CreateDirectory(Path.Combine(htmlPath, "structs"));

                // Clear directory
                Console.WriteLine($"\tRemoving existing files");
                foreach (var f in Directory.GetFiles(htmlPath, "*", SearchOption.AllDirectories))
                {
                    File.Delete(f);
                }

                Console.WriteLine($"\tLoading documentation files");
                DocuDB db = DocuDB.LoadDocuDB(game, Path.Combine(options.InputFolder, game.ToString()));

                Console.WriteLine($"\tGenerating class html");
                foreach (var classD in db.ClassDocumentation)
                {
                    OutputClassDocumentation(htmlPath, db, classD);
                }

                GenerateIndexPage(htmlPath, db, "classes");

                Console.WriteLine($"\tGenerating enum html");
                foreach (var enumD in db.EnumDocumentation)
                {
                    OutputEnumDocumentation(htmlPath, db, enumD);
                }
                GenerateIndexPage(htmlPath, db, "enums");

                Console.WriteLine($"\tGenerating struct html");
                foreach (var structD in db.StructDocumentation)
                {
                    OutputStructDocumentation(htmlPath, db, structD);
                }
                GenerateIndexPage(htmlPath, db, "structs");


                File.Copy(Path.Combine(AppContext.BaseDirectory, "css", "lexdoc.css"), Path.Combine(htmlPath, "lexdoc.css"));
            }
        }

        private static void GenerateIndexPage(string htmlPath, DocuDB db, string subpath)
        {
            var inputPath = Path.Combine(htmlPath, subpath);
            var outputPath = Path.Combine(inputPath, "index.html");

            var html = HTML_TEMPLATE;
            var index = INDEX_TEMPLATE;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<ul>");
            switch (subpath)
            {
                case "classes":
                    foreach (var cls in db.ClassDocumentation)
                    {
                        sb.AppendLine($"<li><a data-type=\"class\">{cls.Key}</a></li>");
                    }
                    break;
                case "structs":
                    foreach (var cls in db.StructDocumentation)
                    {
                        sb.AppendLine($"<li><a data-type=\"struct\">{cls.Key}</a></li>");
                    }
                    break;
                case "enums":
                    foreach (var cls in db.EnumDocumentation)
                    {
                        sb.AppendLine($"<li><a data-type=\"enum\">{cls.Key}</a></li>");
                    }
                    break;
            }
            sb.AppendLine("<;ul>");

            index = index.Replace("%LD_LIST%", sb.ToString());
            index = index.Replace("%LD_LISTTYPE%", subpath.UpperFirst());


            html = html.Replace("%LD_CONTENT%", index);
            html = html.Replace("%LD_RELPATH%", "../");
            html = html.Replace("%LD_GAME%", db.Game.ToString());
            html = html.Replace("%LD_TITLE%", $"All {subpath.UpperFirst()}");
            html = html.Replace("%LD_DESCRIPTION%", $"List of all {subpath} in {db.Game}.");

            File.WriteAllText(outputPath, html);
        }

        private static string HTML_TEMPLATE;
        private static string INDEX_TEMPLATE;
        private static string CLASS_TEMPLATE;
        private static string FUNCTIONROW_TEMPLATE;
        private static string FUNCTIONSPEC_TEMPLATE;
        private static string ENUM_TEMPLATE;
        private static string STRUCT_TEMPLATE;
        private static string MEMBER_TEMPLATE;

        private static void LoadTemplates()
        {
            INDEX_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "index.lextd"));
            HTML_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Html.lextd"));
            CLASS_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Class.lextd"));
            MEMBER_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "MemberRow.lextd"));
            FUNCTIONROW_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "FunctionRow.lextd"));
            FUNCTIONSPEC_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "FunctionSpec.lextd"));
            ENUM_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Enum.lextd"));
            STRUCT_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Struct.lextd"));
        }

        private static void OutputClassDocumentation(string htmlPath, DocuDB db, KeyValuePair<string, DocuClassEntry> classD)
        {
            var outFile = Path.Combine(htmlPath, "classes", $"{classD.Key}.html");

            var html = HTML_TEMPLATE;


            var content = CLASS_TEMPLATE;

            var inheritanceTree = BuildInheritanceTree(db, classD.Key);

            content = content.Replace("%LD_INHERITANCETREE%", inheritanceTree);
            content = content.Replace("%LD_CLASSNAME%", classD.Key);

            #region VARIABLES

            {
                StringBuilder memberHtml = new StringBuilder();
                string rowClass = "row";
                foreach (var member in classD.Value.Variables)
                {
                    var mrow = MEMBER_TEMPLATE;

                    string typeText = GetTypeText(db, member.Value.MemberType);


                    mrow = mrow.Replace("%LD_MEMBERTYPE%", typeText);
                    mrow = mrow.Replace("%LD_MEMBERNAME%", member.Key);
                    mrow = mrow.Replace("%LD_MEMBERDOC%", member.Value.MemberDocumentation);

                    // Formatting
                    mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                    memberHtml.AppendLine(mrow);

                    if (rowClass == "row")
                    {
                        rowClass = "alt";
                    }
                    else
                    {
                        rowClass = "row";
                    }
                }

                content = content.Replace("%LD_MEMBERLIST%", memberHtml.ToString());
            }
            #endregion

            #region FUNCTIONS

            {
                StringBuilder functionHtml = new StringBuilder();
                string rowClass = "row";
                foreach (var member in classD.Value.Functions)
                {
                    var mrow = FUNCTIONROW_TEMPLATE;


                    string returnTypeText = member.Value.ReturnType;
                    if (db.ClassDocumentation.TryGetValue(member.Value.ReturnType, out var ci))
                    {
                        returnTypeText = $"<a href=\"{member.Value.ReturnType}.html\">{member.Value.ReturnType}</a>";
                    }

                    mrow = mrow.Replace("%LD_FUNCTIONRETURNTYPE%", returnTypeText);

                    var sig = member.Value.FunctionSignature;
                    if (member.Value.ReturnType != "None")
                    {
                        // Remove return type from signature
                        sig = sig.Substring(sig.IndexOf(' ') + 1);
                    }

                    var buildingSig = sig[..(sig.IndexOf('(') + 1)];

                    // HTML-ify the params.
                    // This is a hack.
                    var parms = sig[(sig.IndexOf('(') + 1)..^1].Split(',');
                    foreach (var parm in parms)
                    {
                        var parm2 = parm.Trim();
                        // Debug.WriteLine(parm2);
                        var words = parm2.Split(' ');

                        string param = "";
                        foreach (var word in words)
                        {
                            param += " " + GetTypeText(db, word);
                        }

                        buildingSig += param.Trim() + ", ";
                    }

                    buildingSig = buildingSig.TrimEnd(',', ' ');
                    buildingSig += ')';


                    mrow = mrow.Replace("%LD_FUNCTIONSIGNATURE%", buildingSig);
                    mrow = mrow.Replace("%LD_FUNCTIONDOC%", member.Value.MemberDocumentation);

                    // Formatting
                    mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                    functionHtml.AppendLine(mrow);

                    if (rowClass == "row")
                    {
                        rowClass = "alt";
                    }
                    else
                    {
                        rowClass = "row";
                    }
                }

                content = content.Replace("%LD_FUNCTIONLIST%", functionHtml.ToString());
            }

            #endregion


            // Install content
            html = html.Replace("%LD_CONTENT%", content);

            // Shared on page
            html = html.Replace("%LD_TITLE%", classD.Key);
            html = html.Replace("%LD_DESCRIPTION%", classD.Value.ClassDocumentation);
            html = html.Replace("%LD_GAME%", db.Game.ToString());

            // We are one folder deep.
            html = html.Replace("%LD_RELPATH%", "../");

            File.WriteAllText(outFile, html);
        }

        /// <summary>
        /// Looks at database to see if the given value is a member type we can link to
        /// </summary>
        /// <param name="valueMemberType"></param>
        /// <returns></returns>
        private static string GetTypeText(DocuDB db, string type)
        {
            if (db.ClassDocumentation.TryGetValue(type, out var ci))
            {
                return $"<a data-type=\"class\">{type}</a>";
            }

            if (db.EnumDocumentation.TryGetValue(type, out var ei))
            {
                return $"<a data-type=\"enum\">{type}</a>";
            }

            if (db.StructDocumentation.TryGetValue(type, out var si))
            {
                return $"<a data-type=\"struct\">{type}</a>";
            }

            return type;
        }

        private static string? BuildInheritanceTree(DocuDB db, string classKey)
        {
            db.ClassDocumentation.TryGetValue(classKey, out var classInfo);
            Stack<string> classStack = new Stack<string>();
            classStack.Add(classKey);
            while (classInfo != null)
            {
                var parentName = classInfo.ParentClass;
                if (parentName == null || !db.ClassDocumentation.TryGetValue(parentName, out classInfo))
                {
                    break;
                }
                classStack.Add(parentName);
            }

            return BuildInheritanceItem(db, classStack);
        }

        private static string BuildInheritanceItem(DocuDB db, Stack<string> items)
        {
            if (items.TryPop(out var item))
            {
                var info = db.ClassDocumentation[item];
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<ul class=\"inheritance\">");
                sb.AppendLine($"<li><a href=\"{item}.html\">{info.Package}.{item}</a></li>");

                var sub = BuildInheritanceItem(db, items);
                if (sub != null)
                {
                    sb.AppendLine("<li>");
                    sb.AppendLine(sub);
                    sb.AppendLine("</li>");
                }

                sb.AppendLine("</ul>");
                return sb.ToString();
            }

            return null;
        }

        private static void OutputStructDocumentation(string htmlPath, DocuDB db, KeyValuePair<string, DocuStructEntry> structD)
        {
            var outFile = Path.Combine(htmlPath, "structs", $"{structD.Key}.html");

            var html = HTML_TEMPLATE;


            var content = STRUCT_TEMPLATE;

            // Can structs inherit...?
            //var inheritanceTree = BuildInheritanceTree(db, structD.Key);
            //content = content.Replace("%LD_INHERITANCETREE%", inheritanceTree);
            //content = content.Replace("%LD_CLASSNAME%", structD.Key);

            #region VARIABLES
            {
                StringBuilder memberHtml = new StringBuilder();
                string rowClass = "row";
                foreach (var member in structD.Value.Members)
                {
                    var mrow = MEMBER_TEMPLATE;

                    string typeText = GetTypeText(db, member.Value.MemberType);


                    mrow = mrow.Replace("%LD_MEMBERTYPE%", typeText);
                    mrow = mrow.Replace("%LD_MEMBERNAME%", member.Key);
                    mrow = mrow.Replace("%LD_MEMBERDOC%", member.Value.MemberDocumentation);

                    // Formatting
                    mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                    memberHtml.AppendLine(mrow);

                    if (rowClass == "row")
                    {
                        rowClass = "alt";
                    }
                    else
                    {
                        rowClass = "row";
                    }
                }

                content = content.Replace("%LD_MEMBERLIST%", memberHtml.ToString());
            }
            #endregion

            content = content.Replace("%LD_STRUCTNAME%", structD.Key);


            // Install content
            html = html.Replace("%LD_CONTENT%", content);

            // Shared on page
            html = html.Replace("%LD_TITLE%", structD.Key);
            html = html.Replace("%LD_DESCRIPTION%", structD.Value.MemberDocumentation);
            html = html.Replace("%LD_GAME%", db.Game.ToString());

            // We are one folder deep.
            html = html.Replace("%LD_RELPATH%", "../");

            File.WriteAllText(outFile, html);

        }

        private static void OutputEnumDocumentation(string htmlPath, DocuDB db, KeyValuePair<string, DocuEnumEntry> enumD)
        {
            var outFile = Path.Combine(htmlPath, "enums", $"{enumD.Key}.html");

            var html = HTML_TEMPLATE;

            var content = ENUM_TEMPLATE;

            #region VALUES
            {
                StringBuilder memberHtml = new StringBuilder();
                string rowClass = "row";
                foreach (var member in enumD.Value.EnumValues)
                {
                    var mrow = MEMBER_TEMPLATE;

                    // MEMBERTYPE is the first column.
                    mrow = mrow.Replace("%LD_MEMBERTYPE%", member.Value.EnumValue.ToString());
                    mrow = mrow.Replace("%LD_MEMBERNAME%", member.Key);
                    mrow = mrow.Replace("%LD_MEMBERDOC%", member.Value.MemberDocumentation);

                    // Formatting
                    mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                    memberHtml.AppendLine(mrow);

                    if (rowClass == "row")
                    {
                        rowClass = "alt";
                    }
                    else
                    {
                        rowClass = "row";
                    }
                }

                content = content.Replace("%LD_MEMBERLIST%", memberHtml.ToString());
            }
            #endregion

            content = content.Replace("%LD_ENUMNAME%", enumD.Key);

            // Install content
            html = html.Replace("%LD_CONTENT%", content);

            // Shared on page
            html = html.Replace("%LD_TITLE%", enumD.Key);
            html = html.Replace("%LD_DESCRIPTION%", enumD.Value.MemberDocumentation);
            html = html.Replace("%LD_GAME%", db.Game.ToString());

            // We are one folder deep.
            html = html.Replace("%LD_RELPATH%", "../");

            File.WriteAllText(outFile, html);

        }
    }
}
