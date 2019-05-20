namespace Tekapo
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using Neovolve.Windows.Forms;
    using Neovolve.Windows.Forms.Controls;
    using Tekapo.Controls;
    using Tekapo.Properties;

    /// <summary>
    ///     The <see cref="MainForm" /> class is the main application window for Tekapo. It hosts the pages in the wizard
    ///     process that allows
    ///     the user to either rename or time-shift image files.
    /// </summary>
    public partial class MainForm : WizardForm
    {
        private readonly IExecutionContext _executionContext;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MainForm" /> class.
        /// </summary>
        public MainForm(IExecutionContext executionContext, IList<WizardPage> pages)
        {
            _executionContext = executionContext;

            InitializeComponent();

            // Populate the state
            PopulateState();

            // Populate pages
            PopulateWizardPages(pages);
        }

        /// <summary>
        ///     Raises the <see cref="WizardForm.Navigating" /> and <see cref="WizardForm.Navigated" /> events.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="WizardFormNavigationEventArgs" /> instance containing the event data.
        /// </param>
        protected override void OnNavigate(WizardFormNavigationEventArgs e)
        {
            if (e == null)
            {
                throw new ArgumentNullException(nameof(e));
            }

            if (e.NavigationType == WizardFormNavigationType.Help)
            {
                // Shell the help page
                Process.Start(Resources.HelpAddress);
            }
            else
            {
                base.OnNavigate(e);
            }
        }

        /// <summary>
        ///     Adds the item to MRU.
        /// </summary>
        /// <param name="list">
        ///     The list to add the item to.
        /// </param>
        /// <param name="newItem">
        ///     The new item.
        /// </param>
        /// <param name="maxListSize">
        ///     Size of the max list.
        /// </param>
        private static void AddItemToMru(IList<string> list, string newItem, int maxListSize)
        {
            if (list.Contains(newItem))
            {
                // Remove the item
                list.Remove(newItem);
            }

            // Insert the item at the start of the list
            list.Insert(0, newItem);

            // Loop while there are too many items
            while (list.Count > maxListSize)
            {
                // Remove the last item
                list.RemoveAt(list.Count - 1);
            }
        }

        /// <summary>
        ///     Handles the FormClosing event of the MainForm control.
        /// </summary>
        /// <param name="sender">
        ///     The source of the event.
        /// </param>
        /// <param name="e">
        ///     The <see cref="System.Windows.Forms.FormClosingEventArgs" /> instance containing the event data.
        /// </param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Store state
            StoreStateValues();

            // Attempt to delete the log path if it exists
            var path = (string) State[Constants.ResultsLogPathStateKey];

            if (string.IsNullOrEmpty(path) == false
                && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        ///     Handles the Load event of the MainForm control.
        /// </summary>
        /// <param name="sender">
        ///     The source of the event.
        /// </param>
        /// <param name="e">
        ///     The <see cref="System.EventArgs" /> instance containing the event data.
        /// </param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Assign the window title using the application name and version
            Text = Application.ProductName;
        }

        /// <summary>
        ///     Populates the state.
        /// </summary>
        private void PopulateState()
        {
            State[Constants.CommandLineArgumentsProcessedStateKey] = false;
            State[Constants.TaskStateKey] = Constants.RenameTask;

            // Determine whether there is a directory path in the commandline arguments
            var searchPath = _executionContext.SearchPath;

            // Check if there is a search path
            if (string.IsNullOrEmpty(searchPath))
            {
                searchPath = Settings.Default.LastSearchDirectory;

                // Check if there is a search path
                if (string.IsNullOrEmpty(searchPath))
                {
                    // Set the search path to the personal directory
                    searchPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                }
            }

            State[Constants.SearchPathStateKey] = searchPath;
            State[Constants.SearchSubDirectoriesStateKey] = Settings.Default.SearchSubDirectories;

            try
            {
                State[Constants.SearchFilterTypeStateKey] = Enum.Parse(
                    typeof(SearchFilterType),
                    Settings.Default.SearchFilterType);
            }
            catch (ArgumentException)
            {
                // An invalid enum value was defined
                State[Constants.SearchFilterTypeStateKey] = SearchFilterType.None;
            }

            State[Constants.SearchFilterRegularExpressionStateKey] = Settings.Default.RegularExpressionSearchFilter;
            State[Constants.SearchFilterWildcardStateKey] = Settings.Default.WildcardSearchFilter;

            var lastNameFormat = Settings.Default.NameFormat;

            // Check if there is a name format
            if (string.IsNullOrEmpty(lastNameFormat))
            {
                // Set a default name format
                lastNameFormat = Resources.DefaultRenameFormat;
            }

            State[Constants.NameFormatStateKey] = lastNameFormat;
            State[Constants.IncrementOnCollisionStateKey] = Settings.Default.IncrementOnCollision;

            // Determine the MRU list for search directories
            State[Constants.SearchDirectoryMRUStateKey] = Helper.DeserializeList(Settings.Default.SearchDirectoryMRU);

            // Determine the MRU list for name formats
            State[Constants.NameFormatMRUStateKey] = Helper.DeserializeList(Settings.Default.NameFormatMRU);
        }

        /// <summary>
        ///     Populates the wizard pages.
        /// </summary>
        /// <param name="pages"></param>
        [SuppressMessage("Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Pages are disposed when the form is disposed.")]
        private void PopulateWizardPages(IList<WizardPage> pages)
        {
            // Create the Choose Task page
            var chooseTaskPage = pages.OfType<ChooseTaskPage>().Single();

            Pages.Add(Constants.ChooseNavigationKey, chooseTaskPage);

            // Create the Select Path page
            var selectPathPage = pages.OfType<SelectPathPage>().Single();
            var selectPathSkipButtonSettings = new WizardButtonSettings(Resources.SkipButtonText);
            var selectPathPageSettings = new WizardPageSettings(null, null, null, null, selectPathSkipButtonSettings);
            var selectPathNavigationSettings = new WizardPageNavigationSettings(
                null,
                null,
                null,
                null,
                Constants.SelectFilesNavigationKey);

            Pages.Add(Constants.SelectPathNavigationKey,
                selectPathPage,
                selectPathPageSettings,
                selectPathNavigationSettings);

            // Create the File Search page
            var fileSearchPage = pages.OfType<FileSearchPage>().Single();

            Pages.Add(Constants.FileSearchNavigationKey, fileSearchPage);

            // Create the Select Files page
            var selectFilesPage = pages.OfType<SelectFilesPage>().Single();
            var selectFilesNavigationSettings =
                new WizardPageNavigationSettings(null, Constants.SelectPathNavigationKey);

            Pages.Add(Constants.SelectFilesNavigationKey, selectFilesPage, null, selectFilesNavigationSettings);

            // Declare the finish page settings
            var nameFormatPage = pages.OfType<NameFormatPage>().Single();
            var finishButton = new WizardButtonSettings(Resources.Finish);
            var finishPageSettings = new WizardPageSettings(finishButton);
            var finishNavigationSettings = new WizardPageNavigationSettings(Constants.ProcessFilesNavigationKey);

            // Create the Naming Format page
            Pages.Add(Constants.NameFormatNavigationKey, nameFormatPage, finishPageSettings, finishNavigationSettings);

            // Create the Time Shift page
            var timeShiftPage = pages.OfType<TimeShiftPage>().Single();

            Pages.Add(Constants.TimeShiftNavigationKey, timeShiftPage, finishPageSettings, finishNavigationSettings);

            // Create the Progress page
            var processFilesPage = pages.OfType<ProcessFilesPage>().Single();

            Pages.Add(Constants.ProcessFilesNavigationKey, processFilesPage);

            var completedPage = pages.OfType<CompletedPage>().Single();

            completedPage.PageSettings.BackButtonSettings.Visible = false;
            completedPage.PageSettings.NextButtonSettings.Text = Resources.Close;
            completedPage.PageSettings.CancelButtonSettings.Visible = false;
            completedPage.PageSettings.CustomButtonSettings.Text = Resources.Restart;
            completedPage.PageSettings.CustomButtonSettings.Visible = true;
            completedPage.NavigationSettings.CustomPageKey = Constants.ChooseNavigationKey;

            Pages.Add(Constants.CompletedNavigationKey, completedPage);
        }

        /// <summary>
        ///     Stores the state values.
        /// </summary>
        private void StoreStateValues()
        {
            // SelectPathPage state values
            Settings.Default.LastSearchDirectory = (string) State[Constants.SearchPathStateKey];
            Settings.Default.SearchSubDirectories = (bool) State[Constants.SearchSubDirectoriesStateKey];
            Settings.Default.SearchFilterType = State[Constants.SearchFilterTypeStateKey].ToString();
            Settings.Default.RegularExpressionSearchFilter =
                (string) State[Constants.SearchFilterRegularExpressionStateKey];
            Settings.Default.WildcardSearchFilter = (string) State[Constants.SearchFilterWildcardStateKey];

            // NameFormatPage state values
            Settings.Default.NameFormat = (string) State[Constants.NameFormatStateKey];
            Settings.Default.IncrementOnCollision = (bool) State[Constants.IncrementOnCollisionStateKey];

            // Store the search directory MRU
            var searchDirectoryMRU = (BindingList<string>) State[Constants.SearchDirectoryMRUStateKey];
            AddItemToMru(searchDirectoryMRU,
                Settings.Default.LastSearchDirectory,
                Settings.Default.MaxSearchDirectoryMRUItems);
            Settings.Default.SearchDirectoryMRU = Helper.SerializeList(searchDirectoryMRU);

            // Store the name format MRU
            var nameFormatMRU = (BindingList<string>) State[Constants.NameFormatMRUStateKey];
            AddItemToMru(nameFormatMRU, Settings.Default.NameFormat, Settings.Default.MaxNameFormatMRUItems);
            Settings.Default.NameFormatMRU = Helper.SerializeList(nameFormatMRU);

            // Save the properties
            Settings.Default.Save();
        }
    }
}