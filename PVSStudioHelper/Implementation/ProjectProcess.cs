using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace PVSStudioHelper.Implementation
{
    static class ProjectProcess
    {
        public static async Task ProcessAsync(DTE dte, Report report, bool addComment, Func<string,Task> writeAction)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            foreach (Project project in dte.Solution.Projects)
            {
                writeAction?.Invoke($"Start to process {project.Name}");
                await SourceCodeProcess.ProcessAsync(project, report, addComment, writeAction);
            }
        }
    }
}
