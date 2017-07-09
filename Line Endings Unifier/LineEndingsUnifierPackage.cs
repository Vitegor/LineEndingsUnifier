﻿using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace JakubBielawa.LineEndingsUnifier
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidLine_Endings_UnifierPkgString)]
    [ProvideOptionPage(typeof(OptionsPage), "Line Endings Unifier", "General Settings", 0, 0, true)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    public sealed class LineEndingsUnifierPackage : Package
    {
        public LineEndingsUnifierPackage()
        {
        }
        
        protected override void Initialize()
        {
            base.Initialize();

            documentEvents = IDE.Events.DocumentEvents;
            documentEvents.DocumentSaved += documentEvents_DocumentSaved;

            var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if ( null != mcs )
            {
                var menuCommandID = new CommandID(GuidList.guidLine_Endings_UnifierCmdSet_File, (int)PkgCmdIDList.cmdidUnifyLineEndings_File);
                var menuItem = new MenuCommand(new EventHandler(UnifyLineEndingsInFileEventHandler), menuCommandID);
                menuItem.Visible = true;
                menuItem.Enabled = true;
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidLine_Endings_UnifierCmdSet_Folder, (int)PkgCmdIDList.cmdidUnifyLineEndings_Folder);
                menuItem = new MenuCommand(new EventHandler(UnifyLineEndingsInFolderEventHandler), menuCommandID);
                menuItem.Visible = true;
                menuItem.Enabled = true;
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidLine_Endings_UnifierCmdSet_Project, (int)PkgCmdIDList.cmdidUnifyLineEndings_Project);
                menuItem = new MenuCommand(new EventHandler(UnifyLineEndingsInProjectEventHandler), menuCommandID);
                menuItem.Visible = true;
                menuItem.Enabled = true;
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidLine_Endings_UnifierCmdSet_Solution, (int)PkgCmdIDList.cmdidUnifyLineEndings_Solution);
                menuItem = new MenuCommand(new EventHandler(UnifyLineEndingsInSolutionEventHandler), menuCommandID);
                menuItem.Visible = true;
                menuItem.Enabled = true;
                mcs.AddCommand(menuItem);
            }

            SetupOutputWindow();
        }

        void documentEvents_DocumentSaved(Document document)
        {
            if (!this.isUnifyingLocked)
            {
                if (this.OptionsPage.ForceDefaultLineEndingOnSave)
                {
                    var currentDocument = document;
                    var textDocument = currentDocument.Object("TextDocument") as TextDocument;
                    var lineEndings = this.DefaultLineEnding;
                    var tmp = 0;

                    var supportedFileFormats = this.SupportedFileFormats;
                    var supportedFileNames = this.SupportedFileNames;

                    if (currentDocument.Name.EndsWithAny(supportedFileFormats) || currentDocument.Name.EqualsAny(supportedFileNames))
                    {
                        var numberOfIndividualChanges = 0;
                        var numberOfAllLineEndings = 0;
                        Output("Unifying started...\n");
                        UnifyLineEndingsInDocument(textDocument, lineEndings, ref tmp, out numberOfIndividualChanges, out numberOfAllLineEndings);
                        Output(string.Format("{0}: changed {1} out of {2} line endings\n", currentDocument.FullName, numberOfIndividualChanges, numberOfAllLineEndings));
                        Output("Done\n");
                    }

                    this.isUnifyingLocked = true;
                    document.Save();
                    this.isUnifyingLocked = false;
                }
            }
        }

        private void UnifyLineEndingsInFileEventHandler(object sender, EventArgs e)
        {
            UnifyLineEndingsInFile();
        }

        private void UnifyLineEndingsInFile()
        {
            var selectedItem = this.IDE.SelectedItems.Item(1);
            var item = selectedItem.ProjectItem;

            var choiceWindow = new LineEndingChoice(item.Name, this.DefaultLineEnding);
            if (choiceWindow.ShowDialog() == true && choiceWindow.LineEndings != LineEndingsChanger.LineEndings.None)
            {
                var supportedFileFormats = this.SupportedFileFormats;
                var supportedFileNames = this.SupportedFileNames;

                if (item.Name.EndsWithAny(supportedFileFormats) || item.Name.EqualsAny(supportedFileNames))
                {
                    System.Threading.Tasks.Task.Run(() =>
                        {
                            Output("Unifying started...\n");
                            var numberOfChanges = 0;
                            var stopWatch = new Stopwatch();
                            stopWatch.Start();
                            UnifyLineEndingsInProjectItem(item, choiceWindow.LineEndings, ref numberOfChanges);
                            stopWatch.Stop();
                            var secondsElapsed = stopWatch.ElapsedMilliseconds / 1000.0;
                            VsShellUtilities.ShowMessageBox(this, string.Format("Successfully changed {0} line endings in {1} seconds!", numberOfChanges, secondsElapsed), "Success",
                                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                            Output(string.Format("Done in {0} seconds\n", secondsElapsed));
                        });
                }
                else
                {
                    VsShellUtilities.ShowMessageBox(this, "This is not a valid source file!", "Error",
                        OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
        }

        private void UnifyLineEndingsInFolderEventHandler(object sender, EventArgs e)
        {
            UnifyLineEndingsInFolder();
        }

        private void UnifyLineEndingsInFolder()
        {
            var selectedItem = IDE.SelectedItems.Item(1);
            var projectItem = selectedItem.ProjectItem;

            var choiceWindow = new LineEndingChoice(selectedItem.Name, this.DefaultLineEnding);
            if (choiceWindow.ShowDialog() == true && choiceWindow.LineEndings != LineEndingsChanger.LineEndings.None)
            {
                System.Threading.Tasks.Task.Run(() =>
                    {
                        Output("Unifying started...\n");
                        var numberOfChanges = 0;
                        var stopWatch = new Stopwatch();
                        stopWatch.Start();
                        UnifyLineEndingsInProjectItems(projectItem.ProjectItems, choiceWindow.LineEndings, ref numberOfChanges);
                        stopWatch.Stop();
                        var secondsElapsed = stopWatch.ElapsedMilliseconds / 1000.0;
                        VsShellUtilities.ShowMessageBox(this, string.Format("Successfully changed {0} line endings in {1} seconds!", numberOfChanges, secondsElapsed), "Success",
                                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        Output(string.Format("Done in {0} seconds\n", secondsElapsed));
                    });
            }
        }

        private void UnifyLineEndingsInProjectEventHandler(object sender, EventArgs e)
        {
            UnifyLineEndingsInProject();
        }

        private void UnifyLineEndingsInProject()
        {
            var selectedItem = this.IDE.SelectedItems.Item(1);
            var selectedProject = selectedItem.Project;

            var choiceWindow = new LineEndingChoice(selectedProject.Name, this.DefaultLineEnding);
            if (choiceWindow.ShowDialog() == true && choiceWindow.LineEndings != LineEndingsChanger.LineEndings.None)
            {
                System.Threading.Tasks.Task.Run(() =>
                    {
                        Output("Unifying started...\n");
                        var numberOfChanges = 0;
                        var stopWatch = new Stopwatch();
                        stopWatch.Start();
                        UnifyLineEndingsInProjectItems(selectedProject.ProjectItems, choiceWindow.LineEndings, ref numberOfChanges);
                        stopWatch.Stop();
                        var secondsElapsed = stopWatch.ElapsedMilliseconds / 1000.0;
                        VsShellUtilities.ShowMessageBox(this, string.Format("Successfully changed {0} line endings in {1} seconds!", numberOfChanges, secondsElapsed), "Success",
                                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                        Output(string.Format("Done in {0} seconds\n", secondsElapsed));
                    });
            }
        }

        private void UnifyLineEndingsInSolutionEventHandler(object sender, EventArgs e)
        {
            UnifyLineEndingsInSolution();
        }

        private void UnifyLineEndingsInSolution(bool askForLineEnding = true)
        {
            var currentSolution = this.IDE.Solution;

            var properties = currentSolution.Properties;
            foreach (Property property in properties)
            {
                if (property.Name == "Name")
                {
                    if (askForLineEnding)
                    {
                        var choiceWindow = new LineEndingChoice((property as Property).Value.ToString(), this.DefaultLineEnding);
                        if (choiceWindow.ShowDialog() == true && choiceWindow.LineEndings != LineEndingsChanger.LineEndings.None)
                        {
                            System.Threading.Tasks.Task.Run(() =>
                                {
                                    Output("Unifying started...\n");
                                    var stopWatch = new Stopwatch();
                                    stopWatch.Start();
                                    var numberOfChanges = 0;
                                    foreach (Project project in currentSolution.GetAllProjects())
                                    {
                                        UnifyLineEndingsInProjectItems(project.ProjectItems, choiceWindow.LineEndings, ref numberOfChanges);
                                    }
                                    stopWatch.Stop();
                                    var secondsElapsed = stopWatch.ElapsedMilliseconds / 1000.0;
                                    VsShellUtilities.ShowMessageBox(this, string.Format("Successfully changed {0} line endings in {1} seconds!", numberOfChanges, secondsElapsed), "Success",
                                        OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                                    Output(string.Format("Done in {0} seconds\n", secondsElapsed));
                                });
                        }
                    }
                    else
                    {
                        var lineEndings = this.DefaultLineEnding;

                        Output("Unifying started...\n");
                        int numberOfChanges = 0;
                        foreach (Project project in currentSolution.Projects)
                        {
                            UnifyLineEndingsInProjectItems(project.ProjectItems, lineEndings, ref numberOfChanges, true);
                        }
                        Output("Done\n");
                    }
                    break;
                }
            }
        }

        private void UnifyLineEndingsInProjectItems(ProjectItems projectItems, LineEndingsChanger.LineEndings lineEndings, ref int numberOfChanges, bool saveAllWasHit = false)
        {
            var supportedFileFormats = this.SupportedFileFormats;
            var supportedFileNames = this.SupportedFileNames;

            foreach (ProjectItem item in projectItems)
            {
                if (item.ProjectItems != null && item.ProjectItems.Count > 0)
                {
                    UnifyLineEndingsInProjectItems(item.ProjectItems, lineEndings, ref numberOfChanges, saveAllWasHit);
                }
                else
                {
                    if (item.Name.EndsWithAny(supportedFileFormats) || item.Name.EqualsAny(supportedFileNames))
                    {
                        UnifyLineEndingsInProjectItem(item, lineEndings, ref numberOfChanges, saveAllWasHit);
                    }
                }
            }
        }

        private void UnifyLineEndingsInProjectItem(ProjectItem item, LineEndingsChanger.LineEndings lineEndings, ref int numberOfChanges, bool saveAllWasHit = false)
        {
            Window documentWindow = null;

            if (!item.IsOpen)
            {
                if (!saveAllWasHit || (saveAllWasHit && !this.OptionsPage.UnifyOnlyOpenFiles))
                {
                    documentWindow = item.Open();
                }
            }
            
            var document = item.Document;
            if (document != null) 
            {
                var numberOfIndividualChanges = 0;
                var numberOfAllLineEndings = 0;

                var textDocument = document.Object("TextDocument") as TextDocument;
                UnifyLineEndingsInDocument(textDocument, lineEndings, ref numberOfChanges, out numberOfIndividualChanges, out numberOfAllLineEndings);
                if (this.OptionsPage.SaveFilesAfterUnifying)
                {
                    this.isUnifyingLocked = true;
                    document.Save();
                    this.isUnifyingLocked = false;
                }

                Output(string.Format("{0}: changed {1} out of {2} line endings\n", document.FullName, numberOfIndividualChanges, numberOfAllLineEndings));
            }

            if (documentWindow != null)
            {
                documentWindow.Close();
            }
        }

        private void UnifyLineEndingsInDocument(TextDocument textDocument, LineEndingsChanger.LineEndings lineEndings, ref int numberOfChanges, out int numberOfIndividualChanges, out int numberOfAllLineEndings)
        {
            var startPoint = textDocument.StartPoint.CreateEditPoint();
            var endPoint = textDocument.EndPoint.CreateEditPoint();

            var text = startPoint.GetText(endPoint.AbsoluteCharOffset);
            var changedText = LineEndingsChanger.ChangeLineEndings(text, lineEndings, ref numberOfChanges, out numberOfIndividualChanges, out numberOfAllLineEndings);
            startPoint.ReplaceText(text.Length, changedText, (int)vsEPReplaceTextOptions.vsEPReplaceTextKeepMarkers);
        }

        private void SetupOutputWindow()
        {
            this.outputWindow = ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            this.guid = new Guid("0F44E2D1-F5FA-4d2d-AB30-22BE8ECD9789");
            var windowTitle = "Line Endings Unifier";
            this.outputWindow.CreatePane(ref this.guid, windowTitle, 1, 1);
        }

        private void Output(string message)
        {
            if (this.OptionsPage.WriteReport)
            {
                IVsOutputWindowPane outputWindowPane;
                this.outputWindow.GetPane(ref this.guid, out outputWindowPane);
            
                outputWindowPane.OutputString(message);
            }
        }

        private bool isUnifyingLocked = false;

        private IVsOutputWindow outputWindow;

        private Guid guid;

        private DocumentEvents documentEvents;

        private LineEndingsChanger.LineEndings DefaultLineEnding
        {
            get { return (LineEndingsChanger.LineEndings)this.OptionsPage.DefaultLineEnding; }
        }

        private string[] SupportedFileFormats
        {
            get { return this.OptionsPage.SupportedFileFormats.Replace(" ", string.Empty).Split(new[] { ';' },  StringSplitOptions.RemoveEmptyEntries); }
        }

        private string[] SupportedFileNames
        {
            get { return this.OptionsPage.SupportedFileNames.Replace(" ", string.Empty).Split(new[] { ';' },  StringSplitOptions.RemoveEmptyEntries); }
        }

        private OptionsPage optionsPage;

        private OptionsPage OptionsPage
        {
            get { return optionsPage ?? (optionsPage = (OptionsPage)GetDialogPage(typeof(OptionsPage))); }
        }

        private DTE ide;

        public DTE IDE
        {
            get { return ide ?? (ide = (DTE)GetService(typeof(DTE))); }
        }
    }
}
