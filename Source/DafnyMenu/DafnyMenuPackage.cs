﻿using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;


namespace DafnyLanguage.DafnyMenu
{
  /// <summary>
  /// This is the class that implements the package exposed by this assembly.
  ///
  /// The minimum requirement for a class to be considered a valid package for Visual Studio
  /// is to implement the IVsPackage interface and register itself with the shell.
  /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
  /// to do it: it derives from the Package class that provides the implementation of the
  /// IVsPackage interface and uses the registration attributes defined in the framework to
  /// register itself and its components with the shell.
  /// </summary>
  // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
  // a package.
  [PackageRegistration(UseManagedResourcesOnly = true)]
  // This attribute is used to register the information needed to show this package
  // in the Help/About dialog of Visual Studio.
  [InstalledProductRegistration("#110", "#112", "1.0")]
  // This attribute is needed to let the shell know that this package exposes some menus.
  [ProvideMenuResource("Menus.ctmenu", 1)]
  [ProvideAutoLoad("{6c7ed99a-206a-4937-9e08-b389de175f68}")]
  [ProvideToolWindow(typeof(BvdToolWindow), Transient = true)]
  [ProvideToolWindowVisibility(typeof(BvdToolWindow), GuidList.guidDafnyMenuCmdSetString)]
  [Guid(GuidList.guidDafnyMenuPkgString)]
  public sealed class DafnyMenuPackage : Package
  {

    private OleMenuCommand compileCommand;
    private OleMenuCommand menuCommand;
    private OleMenuCommand runVerifierCommand;
    private OleMenuCommand stopVerifierCommand;
    private OleMenuCommand toggleSnapshotVerificationCommand;
    private OleMenuCommand showErrorModelCommand;

    /// <summary>
    /// Default constructor of the package.
    /// Inside this method you can place any initialization code that does not require
    /// any Visual Studio service because at this point the package object is created but
    /// not sited yet inside Visual Studio environment. The place to do all the other
    /// initialization is the Initialize method.
    /// </summary>
    public DafnyMenuPackage()
    {
      Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
    }


    #region Package Members

    /// <summary>
    /// Initialization of the package; this method is called right after the package is sited, so this is the place
    /// where you can put all the initialization code that rely on services provided by VisualStudio.
    /// </summary>
    protected override void Initialize()
    {
      Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
      base.Initialize();

      // Add our command handlers for menu (commands must exist in the .vsct file)
      var mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
      if (null != mcs)
      {
        // Create the command for the menu item.
        var compileCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidCompile);
        compileCommand = new OleMenuCommand(CompileCallback, compileCommandID);
        compileCommand.Enabled = false;
        compileCommand.BeforeQueryStatus += compileCommand_BeforeQueryStatus;
        mcs.AddCommand(compileCommand);

        var runVerifierCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidRunVerifier);
        runVerifierCommand = new OleMenuCommand(RunVerifierCallback, runVerifierCommandID);
        runVerifierCommand.Enabled = true;
        runVerifierCommand.BeforeQueryStatus += runVerifierCommand_BeforeQueryStatus;
        mcs.AddCommand(runVerifierCommand);

        var stopVerifierCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidStopVerifier);
        stopVerifierCommand = new OleMenuCommand(StopVerifierCallback, stopVerifierCommandID);
        stopVerifierCommand.Enabled = true;
        stopVerifierCommand.BeforeQueryStatus += stopVerifierCommand_BeforeQueryStatus;
        mcs.AddCommand(stopVerifierCommand);

        var toggleSnapshotVerificationCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidToggleSnapshotVerification);
        DafnyDriver.ToggleIncrementalVerification();
        toggleSnapshotVerificationCommand = new OleMenuCommand(ToggleSnapshotVerificationCallback, toggleSnapshotVerificationCommandID);
        mcs.AddCommand(toggleSnapshotVerificationCommand);

        var showErrorModelCommandID = new CommandID(GuidList.guidDafnyMenuCmdSet, (int)PkgCmdIDList.cmdidShowErrorModel);
        showErrorModelCommand = new OleMenuCommand(ShowErrorModelCallback, showErrorModelCommandID);
        showErrorModelCommand.Enabled = true;
        showErrorModelCommand.BeforeQueryStatus += showErrorModelCommand_BeforeQueryStatus;
        mcs.AddCommand(showErrorModelCommand);

        var menuCommandID = new CommandID(GuidList.guidDafnyMenuPkgSet, (int)PkgCmdIDList.cmdidMenu);
        menuCommand = new OleMenuCommand(new EventHandler((sender, e) => { }), menuCommandID);
        menuCommand.BeforeQueryStatus += menuCommand_BeforeQueryStatus;
        menuCommand.Enabled = true;
        mcs.AddCommand(menuCommand);
      }
    }

    private void ToggleSnapshotVerificationCallback(object sender, EventArgs e)
    {
      var on = DafnyDriver.ToggleIncrementalVerification();
      toggleSnapshotVerificationCommand.Text = (on ? "Disable" : "Enable") + " on-demand re-verification";
    }

    private void StopVerifierCallback(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      DafnyLanguage.ProgressTagger tagger;
      if (dte.ActiveDocument != null && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(dte.ActiveDocument.FullName, out tagger))
      {
        tagger.StopVerification();
      }
    }

    private void RunVerifierCallback(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      DafnyLanguage.ProgressTagger tagger;
      if (dte.ActiveDocument != null && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(dte.ActiveDocument.FullName, out tagger))
      {
        tagger.StartVerification();
      }
    }

    void menuCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      var isActive = dte.ActiveDocument != null
                     && dte.ActiveDocument.FullName.EndsWith(".dfy", StringComparison.OrdinalIgnoreCase);
      menuCommand.Visible = isActive;
      menuCommand.Enabled = isActive;
    }

    void runVerifierCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      DafnyLanguage.ProgressTagger tagger;
      var enabled = dte.ActiveDocument != null
                 && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(dte.ActiveDocument.FullName, out tagger)
                 && tagger != null && tagger.VerificationDisabled;
      runVerifierCommand.Visible = enabled;
      runVerifierCommand.Enabled = enabled;
    }

    void stopVerifierCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      DafnyLanguage.ProgressTagger tagger;
      var disabled = dte.ActiveDocument != null
                    && DafnyLanguage.ProgressTagger.ProgressTaggers.TryGetValue(dte.ActiveDocument.FullName, out tagger)
                    && tagger != null && tagger.VerificationDisabled;
      stopVerifierCommand.Visible = !disabled;
      stopVerifierCommand.Enabled = !disabled;
    }

    void compileCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      ResolverTagger resolver;
      var enabled = dte.ActiveDocument != null
                    && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(dte.ActiveDocument.FullName, out resolver)
                    && resolver.Program != null;
      compileCommand.Enabled = enabled;
    }

    #endregion


    /// <summary>
    /// This function is the callback used to execute a command when the a menu item is clicked.
    /// See the Initialize method to see how the menu item is associated to this function using
    /// the OleMenuCommandService service and the MenuCommand class.
    /// </summary>
    private void CompileCallback(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      ResolverTagger resolver;

      if (dte.ActiveDocument != null
          && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(dte.ActiveDocument.FullName, out resolver)
          && resolver.Program != null)
      {
        IVsStatusbar statusBar = (IVsStatusbar)GetGlobalService(typeof(SVsStatusbar));
        uint cookie = 0;
        statusBar.Progress(ref cookie, 1, "Compiling...", 0, 0);

        DafnyDriver.Compile(resolver.Program);

        statusBar.Progress(ref cookie, 0, "", 0, 0);
      }
    }

    /// <summary>
    /// Show error model.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void showErrorModelCommand_BeforeQueryStatus(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      ResolverTagger resolver;
      var visible = dte.ActiveDocument != null
                    && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(dte.ActiveDocument.FullName, out resolver)
                    && resolver.Program != null
                    && resolver.VerificationErrors.Any(err => !string.IsNullOrEmpty(err.Model));
      showErrorModelCommand.Visible = visible;
    }

    private void ShowErrorModelCallback(object sender, EventArgs e)
    {
      var dte = GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
      ResolverTagger resolver = null;
      var show = dte.ActiveDocument != null
                 && DafnyLanguage.ResolverTagger.ResolverTaggers.TryGetValue(dte.ActiveDocument.FullName, out resolver)
                 && resolver.Program != null
                 && resolver.VerificationErrors.Any(err => err.IsSelected && !string.IsNullOrEmpty(err.Model));
      if (show)
      {
        var window = this.FindToolWindow(typeof(BvdToolWindow), 0, true);
        if ((window == null) || (window.Frame == null))
        {
          throw new NotSupportedException("Can not create BvdToolWindow.");
        }

        var selectedError = resolver.VerificationErrors.FirstOrDefault(err => err.IsSelected && !string.IsNullOrEmpty(err.Model));
        if (selectedError != null)
        {
          BvdToolWindow.BVD.ReadModel(selectedError.Model);
          BvdToolWindow.BVD.SetState(selectedError.SelectedStateId, true);
        }

        IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
        Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
      }
    }
  }
}
