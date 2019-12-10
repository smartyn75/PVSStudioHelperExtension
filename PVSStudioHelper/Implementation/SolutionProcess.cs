using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace PVSStudioHelper.Implementation
{
    static class SolutionProcess
    {
        public static async Task<Report> ProcessAsync(IAsyncServiceProvider serviceProvider, bool addComment)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var report = new Report();

            var dte = (DTE) await serviceProvider.GetServiceAsync(typeof(DTE));
            if (dte == null || string.IsNullOrEmpty(dte.Solution.FullName))
            {
                report.SolutionName = "N/A";
                return report;
            }

            report.SolutionName = System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileName(dte.Solution.FullName));

            IVsStatusbar statusBar = (IVsStatusbar)await serviceProvider.GetServiceAsync(typeof(SVsStatusbar));
            IVsThreadedWaitDialogFactory factory =
                (IVsThreadedWaitDialogFactory) await serviceProvider.GetServiceAsync(
                    typeof(SVsThreadedWaitDialogFactory));
            IVsThreadedWaitDialog2 dialog=null;
            factory?.CreateInstance(out dialog);
            dialog?.StartWaitDialog("PVSStudio Helper", (addComment) ? "Add comment" : "Remove comment", null, null,
                null, 0, false, true);

            IVsOutputWindow outWindow =
                (IVsOutputWindow) await serviceProvider.GetServiceAsync(typeof(SVsOutputWindow));
            var generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane; // P.S. There's also the GUID_OutWindowDebugPane available.
            IVsOutputWindowPane generalPane = null;
            outWindow?.GetPane( ref generalPaneGuid , out generalPane );
            if (generalPane == null)
            {
                outWindow?.CreatePane(ref generalPaneGuid, "General", 1, 0);
                outWindow?.GetPane( ref generalPaneGuid , out generalPane );
            }

            await ProjectProcess.ProcessAsync(dte, report, addComment, async (message)=>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    dialog?.UpdateProgress(null, message, null, 0, 0, true, out var canceled);
                    //messagePump.WaitText = message;
                    if (statusBar != null)
                    {
                        statusBar.IsFrozen(out var isFrozen);
                        if (isFrozen == 0)
                            statusBar.SetText(message);
                    }

                    generalPane?.OutputString($"{message}{Environment.NewLine}");
                });
            var finalMessage =
                $"The solution {report.SolutionName} processed. Processed items: {report.ProcessedItems}, include opened items {report.ProcessedOpenedItems}";
            if (statusBar != null)
            {
                statusBar.IsFrozen(out var isFrozen);
                if (isFrozen == 0)
                    statusBar.SetText(finalMessage);
            }
            generalPane?.OutputStringThreadSafe($"{finalMessage}{Environment.NewLine}");

            dialog.EndWaitDialog();

            return report;
        }
    }
}
