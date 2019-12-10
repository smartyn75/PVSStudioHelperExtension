using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using PVSStudioHelper.Properties;
using Task = System.Threading.Tasks.Task;

namespace PVSStudioHelper.Implementation
{
    static class SourceCodeProcess
    {
        public static async Task ProcessAsync(Project project, Report report, bool addComment,
            Func<string, Task> writeAction)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var queue = new Queue<ProjectItem>(project.ProjectItems.Cast<ProjectItem>());
            var subProjects = new List<Project>();
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                if (item.ProjectItems != null)
                    foreach (var subItem in item.ProjectItems.Cast<ProjectItem>())
                        queue.Enqueue(subItem);
                var kindGuid = Guid.Parse(item.Kind);
                if (kindGuid == VSConstants.GUID_ItemType_PhysicalFile)
                {
                    if (item.FileCodeModel != null)
                    {
                        if (IsCSharpFile(item.FileCodeModel.Language))
                        {
                            if (item.IsOpen && item.Document != null)
                            {
                                TextDocument editDoc = (TextDocument) item.Document.Object("TextDocument");
                                EditPoint objEditPt = editDoc.CreateEditPoint();
                                writeAction?.Invoke($"{item.Document.FullName}");
                                objEditPt.StartOfDocument();
                                item.Document.ReadOnly = false;
                                if (addComment) await AddCommentsAsync(objEditPt);
                                else await RemoveCommentsAsync(objEditPt);
                                report.ProcessedItems += 1;
                                report.ProcessedOpenedItems += 1;
                            }
                            else
                                for (short index = 0; index < item.FileCount; index++)
                                {
                                    writeAction?.Invoke($"{item.FileNames[index]}");
                                    if (item.IsOpen) continue;
                                    if (addComment) AddComments(item.FileNames[index]);
                                    else RemoveComments(item.FileNames[index]);
                                    report.ProcessedItems += 1;
                                }
                        }
                    }

                }

                if (kindGuid == ProjectItem)
                {
                    subProjects.Add(item.SubProject);
                }
            }

            foreach (var subProject in subProjects)
                await ProcessAsync(subProject, report, addComment, writeAction);
        }

        private static async Task RemoveCommentsAsync(EditPoint objEditPt)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var template = GetTemplate();
            int index = 0;
            while (index < template.Count)
            {
                var line = string.Empty;
                if (!objEditPt.AtEndOfDocument)
                    line = objEditPt.GetText(objEditPt.LineLength);
                if (string.Equals(line, template[index]))
                {
                    var newPoint = objEditPt.CreateEditPoint();
                    newPoint.LineDown();
                    if (!newPoint.AtEndOfDocument)
                        newPoint.StartOfLine();
                    objEditPt.Delete(newPoint);
                    index++;
                }
                else
                    break;
            }
        }

        private static void RemoveComments(string fileName)
        {
            var encoding = GetEncoding(fileName);
            var content = File.ReadAllLines(fileName).ToList();
            var template = GetTemplate();
            int maxRow = Math.Min(content.Count, template.Count);
            int templateIndex = 0;
            var changed = false;
            while (templateIndex < maxRow)
            {
                if (string.Equals(content[0], template[templateIndex]))
                {
                    changed = true;
                    content.RemoveAt(0);
                    templateIndex++;
                }
                else
                    break;
            }

            if (changed)
                File.WriteAllText(fileName, string.Join(Environment.NewLine,content), encoding);
        }


        private static void AddComments(string fileName)
        {
            var encoding = GetEncoding(fileName);
            var lines = File.ReadAllText(fileName);
            var content = lines.Split(new[] {Environment.NewLine}, StringSplitOptions.None).ToList();
            var template = GetTemplate();
            int maxRow = Math.Min(content.Count, template.Count);
            int index = 0;
            var changed = false;
            while (index < maxRow)
            {
                if (!string.Equals(content[index], template[index]))
                {
                    content.Insert(index, template[index]);
                    changed = true;
                }

                index++;
            }

            if (changed)
            {
                //File.WriteAllText(fileName, string.Join(Environment.NewLine, content), encoding);
                File.WriteAllLines(fileName,content,encoding);
            }
        }

        private static async Task AddCommentsAsync(EditPoint objEditPt)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            objEditPt.StartOfLine();
            var template = GetTemplate();
            int index = 0;
            while (index < template.Count)
            {
                var line = string.Empty;
                if (!objEditPt.AtEndOfDocument)
                    line = objEditPt.GetText(objEditPt.LineLength);
                if (!string.Equals(line, template[index]))
                {
                    objEditPt.Insert($"{template[index]}{Environment.NewLine}");
                    //objEditPt.LineDown();
                    objEditPt.StartOfLine();
                }
                else
                    objEditPt.LineDown();

                index++;
            }

        }

        private static readonly Guid CShartGuid = Guid.Parse(CodeModelLanguageConstants.vsCMLanguageCSharp);
        private static readonly Guid ProjectItem = Guid.Parse(Constants.vsProjectItemKindSolutionItems);

        private static bool IsCSharpFile(string languageGuid)
        {
            return Guid.Parse(languageGuid) == CShartGuid;
        }

        private static List<string> _template;

        private static List<string> GetTemplate()
        {
            if (_template != null) return _template;
            //_template = File.ReadAllLines(Path.Combine(AssemblyDirectory, "Template.txt")).ToList();
            _template = Resources.Template.Split(new[] {Environment.NewLine}, StringSplitOptions.None).ToList();
            return _template;
        }

        /*private static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }*/

        private static Encoding GetEncoding(string filename)
        {
            // This is a direct quote from MSDN:  
            // The CurrentEncoding value can be different after the first
            // call to any Read method of StreamReader, since encoding
            // autodetection is not done until the first call to a Read method.

            using (var reader = new StreamReader(filename, Encoding.Default, true))
            {
                if (reader.Peek() >= 0) // you need this!
                    reader.Read();

                return reader.CurrentEncoding;
            }
        }
    }
}
