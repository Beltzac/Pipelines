using CSharpDiff.Diffs.Models;
using CSharpDiff.Patches;
using CSharpDiff.Patches.Models;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System.Text;

namespace Common.Utils
{
    public static class OracleDiffUtils
    {
        public static PatchResult GetDiff(string view, string old, string newString)
        {
            //string oldText = "console.log(\"Hello World!\")";
            //string newText = "console.log(\"Hello from Diff2Html!\")";

            //var diffBuilder = new InlineDiffBuilder(new Differ());
            //var diff = diffBuilder.BuildDiffModel(oldText, newText, false, false, new WordChunker());

            //return GenerateGitDiffString("sample.js", diff);

            //            return @"
            //`diff --git a/sample.js b/sample.js
            //index 0000001..0ddf2ba
            //--- a/sample.js
            //+++ b/sample.js
            //@@ -1 +1 @@
            //-console.log(""Hello World!"")
            //+console.log(""Hello from Diff2Html!"")
            //";

            //            string oldText = @"
            //function helloWorld() {
            //    console.log('Hello World!');
            //    return 42;
            //}

            //helloWorld();
            //";

            //            string newText = @"
            //function helloFromDiff2Html() {
            //    console.log('Hello from Diff2Html!');
            //    return 42;
            //}

            //helloFromDiff2Html();
            //";

            //var diffBuilder = new InlineDiffBuilder(new Differ());
            //var diff = diffBuilder.BuildDiffModel(oldText, newText);

            //return GenerateGitDiffString("sample.js", diff);



            var viewNameFormated = $"{view}.SQL";

            var ps = new Patch(new PatchOptions(), new DiffOptions()
            {
                IgnoreWhiteSpace = true
                //NewlineIsToken = true
            });
            var patch = ps.createPatchResult(viewNameFormated, viewNameFormated, NormalizeLineBreaks(old), NormalizeLineBreaks(newString), null, null);


            //var d = new Diff();

            //var diff = d.diff(old, newString);

            return patch;
        }

        public static string Format(PatchResult patchResult)
        {
            var ps = new Patch(new PatchOptions(), new DiffOptions()
            {
                IgnoreWhiteSpace = true
                //NewlineIsToken = true
            });
            return ps.formatPatch(patchResult);
        }

        public static string NormalizeLineBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Replace Windows line breaks (\r\n) with Unix line breaks (\n)
            text = text.Replace("\r\n", "\n");

            // Replace old Mac line breaks (\r) with Unix line breaks (\n)
            text = text.Replace("\r", "\n");

            return text;
        }


        static string GenerateGitDiffString(string fileName, DiffPaneModel diff)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"diff --git a/{fileName} b/{fileName}");
            sb.AppendLine($"index 0000001..0ddf2ba");
            sb.AppendLine($"--- a/{fileName}");
            sb.AppendLine($"+++ b/{fileName}");

            int oldLine = 1;
            int newLine = 1;
            bool headerWritten = false;

            foreach (var line in diff.Lines)
            {
                if (!headerWritten)
                {
                    //sb.AppendLine($"@@ -{oldLine} +{newLine} @@");
                    headerWritten = true;
                }

                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        sb.AppendLine($"+{line.Text}");
                        newLine++;
                        break;
                    case ChangeType.Deleted:
                        sb.AppendLine($"-{line.Text}");
                        oldLine++;
                        break;
                    case ChangeType.Unchanged:
                        sb.AppendLine($" {line.Text}");
                        oldLine++;
                        newLine++;
                        break;
                }
            }

            return sb.ToString();
        }

        public static void CompareViewDefinitions2(/*Dictionary<string, string> devViews, Dictionary<string, string> qaViews*/)
        {
            var diffBuilder = new InlineDiffBuilder(new Differ());


            //var diff = diffBuilder.BuildDiffModel("lakjsdljbsd", "lakjsdljasd");
            //PrintDiff(diff);




            //foreach (var viewName in devViews.Keys)
            //{
            //    if (qaViews.ContainsKey(viewName))
            //    {
            //        if (devViews[viewName] != qaViews[viewName])
            //        {
            //            _logger.LogInformation($"Difference in view: {viewName}");
            //            var diff = diffBuilder.BuildDiffModel(devViews[viewName], qaViews[viewName]);
            //            PrintDiff(diff);
            //        }
            //    }
            //    else
            //    {
            //        _logger.LogInformation($"View {viewName} is present in DEV but not in QA");
            //    }
            //}

            //foreach (var viewName in qaViews.Keys)
            //{
            //    if (!devViews.ContainsKey(viewName))
            //    {
            //        _logger.LogInformation($"View {viewName} is present in QA but not in DEV");
            //    }
            //}
        }

        //public static string CompareViewDefinitions3(string old, string new)
        //{
        //    //var oldText = "Could not reconnect to the server. Reload the page to restore functionality.";
        //    //var newText = "Could not reconnect to the server. Relaad the page to restore functionality.";

        //    var diffBuilder = new SideBySideDiffBuilder(new Differ());
        //    var diff = diffBuilder.BuildDiffModel(oldText, newText, false, false);
        //    return PrintDiff(diff);
        //}

        public static string PrintDiff(SideBySideDiffModel diff)
        {
            var htmlBuilder = new StringBuilder();

            htmlBuilder.AppendLine("<div class=\"container\">");

            // Old text column
            htmlBuilder.AppendLine("<div class=\"column\">");
            foreach (var line in diff.OldText.Lines)
            {
                foreach (var subPiece in line.SubPieces)
                {
                    htmlBuilder.Append(subPiece.Type switch
                    {
                        ChangeType.Inserted => $"<span class=\"inserted\">{subPiece.Text}</span>",
                        ChangeType.Deleted => $"<span class=\"deleted\">{subPiece.Text}</span>",
                        _ => subPiece.Text
                    });
                }
                htmlBuilder.AppendLine("<br>");
            }
            htmlBuilder.AppendLine("</div>");

            // New text column
            htmlBuilder.AppendLine("<div class=\"column\">");
            foreach (var line in diff.NewText.Lines)
            {
                foreach (var subPiece in line.SubPieces)
                {
                    htmlBuilder.Append(subPiece.Type switch
                    {
                        ChangeType.Inserted => $"<span class=\"inserted\">{subPiece.Text}</span>",
                        ChangeType.Deleted => $"<span class=\"deleted\">{subPiece.Text}</span>",
                        _ => subPiece.Text
                    });


                }
                htmlBuilder.AppendLine("<br>");
            }
            htmlBuilder.AppendLine("</div>");

            htmlBuilder.AppendLine("</div>");
            // Write HTML to file
            return htmlBuilder.ToString();
        }



        //public static void PrintDiff(DiffPaneModel diff)
        //{
        //    foreach (var line in diff.Lines)
        //    {
        //        Console.ForegroundColor = ConsoleColor.Blue;
        //        if (line.Position.HasValue) Console.Write(line.Position.Value);
        //        Console.Write('\t');
        //        switch (line.Type)
        //        {
        //            case ChangeType.Inserted:
        //                Console.ForegroundColor = ConsoleColor.Green;
        //                Console.Write("+ ");
        //                break;
        //            case ChangeType.Deleted:
        //                Console.ForegroundColor = ConsoleColor.Red;
        //                Console.Write("- ");
        //                break;
        //            default:
        //                Console.ForegroundColor = ConsoleColor.White;
        //                Console.Write("  ");
        //                break;
        //        }

        //        _logger.LogInformation(line.Text);
        //    }
        //    Console.ResetColor();
        //}
    }
}
