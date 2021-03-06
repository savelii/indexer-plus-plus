﻿// This file is the part of the Indexer++ project.
// Copyright (C) 2016 Anna Krykora <krykoraanna@gmail.com>. All rights reserved.
// Use of this source code is governed by a MIT-style license that can be found in the LICENSE file.

﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CLIInterop;
using Indexer.Controls;
using Microsoft.Win32;
using Control = System.Windows.Forms.Control;
using Point = System.Drawing.Point;

namespace Indexer
{
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
        private DispatcherTimer dispatcherTimer;
        private Dispatcher dispatcher;

        public ObservableCollection<DriveInfo> Drives { get; set; }

        public static RoutedCommand NewSearchWindowCommand = new RoutedCommand();

        private string searchString = string.Empty;

        public string SearchString
        {
            get { return searchString; }
            set
            {
                searchString = value;
                Filter();
            }
        }

        private Visibility filtersVisibility;

        public Visibility FiltersVisibility
        {
            get { return filtersVisibility; }
            set
            {
                filtersVisibility = value;
                OnPropertyChanged("FiltersVisibility");
            }
        }

        private string sizeFrom;

        public string SizeFrom
        {
            get { return sizeFrom; }
            set
            {
                sizeFrom = value;
                Filter();
            }
        }

        private string sizeTo;

        public string SizeTo
        {
            get { return sizeTo; }
            set
            {
                sizeTo = value;
                Filter();
            }
        }

        private DateTime dateFrom;

        public DateTime DateFrom
        {
            get { return dateFrom; }
            set
            {
                dateFrom = value;
                Filter();
            }
        }

        private DateTime dateTo;

        public DateTime DateTo
        {
            get { return dateTo; }
            set
            {
                dateTo = value;
                Filter();
            }
        }

        private string searchDirPath = string.Empty;

        public string SearchDirPath
        {
            get { return searchDirPath; }
            set
            {
                searchDirPath = value;
                OnPropertyChanged("SearchDirPath");
                Filter();
            }
        }

        private bool sizeFilterEnabled;

        public bool SizeFilterEnabled
        {
            get { return sizeFilterEnabled; }
            set
            {
                sizeFilterEnabled = value;
                OnPropertyChanged("SizeFilterEnabled");
                Filter();
            }
        }

        private bool dateFilterEnabled;

        public bool DateFilterEnabled
        {
            get { return dateFilterEnabled; }
            set
            {
                dateFilterEnabled = value;
                OnPropertyChanged("DateFilterEnabled");
                Filter();
            }
        }

        private bool dirFilterEnabled;

        public bool DirFilterEnabled
        {
            get { return dirFilterEnabled; }
            set
            {
                dirFilterEnabled = value;

                // TODO: need to implement filesystem changes listener, in order to update folders (and their content)
                // in the tree correctly.
                //
                //if (dirFilterEnabled)
                //{
                //    ExplorerTreeControl.Visibility = Visibility.Visible;
                //    GridSplitter.Visibility = Visibility.Visible;
                //}
                //else
                //{
                //    ExplorerTreeControl.Visibility = Visibility.Collapsed;
                //    GridSplitter.Visibility = Visibility.Collapsed;
                //}

                OnPropertyChanged("DirFilterEnabled");
                Filter();
            }
        }

        private bool matchCase;

        public bool MatchCase
        {
            get { return matchCase; }
            set
            {
                matchCase = value;
                Filter();
            }
        }

        private bool excludeHiddenAndSystem;

        public bool ExcludeHiddenAndSystem
        {
            get { return excludeHiddenAndSystem; }
            set
            {
                excludeHiddenAndSystem = value;
                Filter();
            }
        }

        private bool excludeFolders;

        public bool ExcludeFolders
        {
            get { return excludeFolders; }
            set
            {
                excludeFolders = value;
                Filter();
            }
        }

        private bool excludeFiles;

        public bool ExcludeFiles
        {
            get { return excludeFiles; }
            set
            {
                excludeFiles = value;
                Filter();
            }
        }

        private IconSizeEnum iconSize;

        public IconSizeEnum IconSize
        {
            get { return iconSize; }
            set
            {
                if (iconSize == value) return;

                // TODO: remove this line after fixing virtualization on Windows 8.1.
                // if (ViewType == ViewType.Details && value != IconSizeEnum.SmallIcon16) return;

                iconSize = value;

                IconProvider.Instance.IconSize = value;
                ThumbnailProvider.Instance.ThumbSize = value;

                DataModel.ResetThumbnailsCache();
                OnPropertyChanged("IconSize");
            }
        }

        private ViewType viewType;

        public ViewType ViewType
        {
            get { return viewType; }
            set
            {
                if (viewType == value) return;
                viewType = value;

                // TODO: remove this after fixing virtualization
                if (value == ViewType.Details)
                {
                    IconSize = IconSizeEnum.SmallIcon16;
                    // OnMenuSmallIconsView_Click(this, new RoutedEventArgs());
                }
                OnPropertyChanged("ViewType");
                OnPropertyChanged("IsNotSmallMenuItemEnabled");
            }
        }

        // TODO: remove this after fixing virtualization on Windows 8.1.
        public object IsNotSmallMenuItemEnabled
        {
            get { return true; } // return ViewType == ViewType.Icons; }
        }

        private bool explorerTreeVisible;

        public bool ExplorerTreeVisible
        {
            get { return explorerTreeVisible; }
            set
            {
                if (explorerTreeVisible == value) return;
                explorerTreeVisible = value;
                OnPropertyChanged("ExplorerTreeVisible");
            }
        }

        public Model DataModel { get; set; }

        private readonly bool initializationFinished;

        public MainWindow()
        {
#if DEBUG
            // System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
#endif

            InitPart1();
            PopulateDrives();

            var selectedDrives = Drives.Where(it => it.IsChecked).Select(it => it.Name);

            DataModel = new Model(selectedDrives, IconProvider.Instance, ThumbnailProvider.Instance);

            InitializeComponent();

            InitPart2();

            initializationFinished = true;

            RowPreviewMouseRightButtonUp = new RelayCommand(DetailsViewRow_PreviewMouseUp);
            FileItemMouseDoubleClick = new RelayCommand(FileItem_MouseDoubleClick);

            // TODO: revive after implementing filesystem changes listener.
            //var searchDirPathProperty = DependencyPropertyDescriptor.FromProperty(
            //    ExplorerTreeControl.SearchDirPathProperty, typeof(ExplorerTreeControl));

            //searchDirPathProperty.AddValueChanged(ExplorerTreeControl, OnSearchDirPathchanged);

            Closed += MainWindow_Closed;

            Filter();

            Log.Instance.Debug("MainWindow: Filter calling finished. NewWindow must be created now.");
        }

        private static void MainWindow_Closed(object sender, EventArgs e)
        {
            // TODO: revive after implementing filesystem changes listener.
            //if (ExplorerTreeControl != null)
            //{
            //    ExplorerTreeControl.Dispose();
            //}
        }

        // TODO: revive after implementing filesystem changes listener.
        //private void OnSearchDirPathchanged(object sender, EventArgs e)
        //{
        //    SearchDirPath = ExplorerTreeControl.SearchDirPath;
        //}

        public Visibility DebugLogWndCommandVisibility
        {
            get
            {
                return SystemConfigFlagsWrapper.Instance().ShowDebugLogWindow
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void InitPart1()
        {
            dispatcher = Application.Current.Dispatcher;

            DateFrom = DateTime.Now.AddDays(-30);
            DateTo = DateTime.Now;

            excludeHiddenAndSystem = false;

            if (SystemConfigFlagsWrapper.Instance().CallWatchChanges)
            {
                dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Tick += CheckUpdatesOnTimerTick;
                dispatcherTimer.Interval = new TimeSpan(0, 0, 5);
                dispatcherTimer.Start();
            }
        }

        private void CheckUpdatesOnTimerTick(object sender, EventArgs eventArgs)
        {
            DataModel.CheckUpdates();
        }

        private void InitPart2()
        {
            var settings = UserSettings.Instance;
            Height = settings.WndHeight;
            Width = settings.WndWidth;
            FiltersVisibility = settings.FiltersVisibility;

            var initialDirPath = CmdArgumentsParser.FilterDirPath;
            if (string.IsNullOrWhiteSpace(initialDirPath))
                return;

            dirFilterEnabled = true;
            filtersVisibility = Visibility.Visible;
            searchDirPath = initialDirPath;

            OnPropertyChanged("");
        }

        private void PopulateDrives()
        {
            var selectedDrives = UserSettings.Instance.SelectedDrives;
            Drives = new ObservableCollection<DriveInfo>();

            var filterDir = CmdArgumentsParser.FilterDirPath;


            foreach (var driveInfo in DrivesManager.GetDrives())
            {
                var drive = driveInfo.Name;
                var isChecked = selectedDrives.Count == 0 || selectedDrives.Contains(drive) ||
                                (!string.IsNullOrEmpty(filterDir) && filterDir[0] == drive);

                var di = new DriveInfo(drive, driveInfo.Label, isChecked);
                Drives.Add(di);
            }

            //DrivesManager.OnDeviceInserted += info => AddDrive(new DriveInfo(info.Name, info.Label));
            //DrivesManager.OnDeviceRemoved += driveName =>
            //    {
            //        var old = Drives.SingleOrDefault(d => d.Name == driveName);
            //        RemoveDrive(old);
            //    };
        }

        private void AddDrive(DriveInfo driveInfo)
        {
            if (dispatcher.CheckAccess())
            {
                Drives.Add(driveInfo);
            }
            else
            {
                dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => AddDrive(driveInfo)));
            }
        }

        private void RemoveDrive(DriveInfo driveInfo)
        {
            if (dispatcher.CheckAccess())
            {
                Drives.Remove(driveInfo);
                Filter();
            }
            else
            {
                dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => RemoveDrive(driveInfo)));
            }
        }

        private void DrivesManager_OnDriveSelectedChanged(char c, bool isChecked)
        {
            var drive = Drives.SingleOrDefault(d => d.Name == c);
            if (drive.IsChecked == isChecked)
                return;
            drive.IsChecked = isChecked;
            Filter();
        }

        private void DrivesMenuItem_CheckToogled(object sender, RoutedEventArgs routedEventArgs)
        {
            var item = (MenuItem) sender;
            if (item == null) return;

            var drive = Drives.SingleOrDefault(driveInfo => driveInfo.Label == (string) item.Header).Name;

            if (item.IsChecked)
            {
                DataModel.OnDriveSelected(drive);
                //DrivesManager.AddSelectedDrive(drive);
            }
            else
            {
                DataModel.OnDriveUnselected(drive);
                //DrivesManager.RemoveSelectedDrive(drive);
            }
        }

        private void Filter()
        {
            if (!initializationFinished) return;
            var q = new SearchQueryWrapper
            {
                Text = SearchString,
                SizeFrom = 0,
                SizeTo = 0,
                CreatedTimeFrom = DateTime.MinValue,
                CreatedTimeTo = DateTime.MinValue
            };

            if (SizeFilterEnabled)
            {
                int minSize;
                if (!Helper.TryParseSize(SizeFrom, out minSize))
                    return;
                q.SizeFrom = minSize < 1 ? 0 : minSize;

                int maxSize;
                if (!Helper.TryParseSize(SizeTo, out maxSize))
                    return;
                q.SizeTo = maxSize;
            }

            if (DirFilterEnabled)
            {
                q.SearchDirPath = string.IsNullOrWhiteSpace(SearchDirPath) ? string.Empty : searchDirPath;
            }

            if (DateFilterEnabled)
            {
                q.CreatedTimeFrom = DateFrom;
                q.CreatedTimeTo = DateTo;
            }

            q.MatchCase = MatchCase;

            q.ExcludeHiddenAndSystem = ExcludeHiddenAndSystem;
            q.ExcludeFolders = ExcludeFolders;
            q.ExcludeFiles = ExcludeFiles;

            DataModel.Filter(q);
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            Log.Instance.Debug("MainWindow_OnClosing called.");

            UserSettings.Instance.Save(this);

            if (SystemConfigFlagsWrapper.Instance().PipeManager)
            {
                Visibility = Visibility.Collapsed;
                if (Equals(Application.Current.MainWindow, this))
                    cancelEventArgs.Cancel = true;
            }
        }

        #region Commands

        private DebugLogWindow debugLogWnd;

        private void ShowDebugLogWndCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = debugLogWnd == null && SystemConfigFlagsWrapper.Instance().ShowDebugLogWindow;
        }

        private void ShowDebugLogWndExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            debugLogWnd = new DebugLogWindow();
            debugLogWnd.Closed += (o, args) => { debugLogWnd = null; };
            debugLogWnd.Show();
        }


        private void SaveAsCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void SaveAsExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.FileName = "Search Result"; // Default file name
            dlg.DefaultExt = ".txt"; // Default file extension
            dlg.Filter = "Text document (.txt)|*.txt|Comma Separated Values (.csv)|*.csv";

            var result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document
                var filename = dlg.FileName;
                var format = string.Empty;
                var contents = new List<string>();

                if (dlg.FilterIndex == 2)
                {
                    format = @"""%p"",""%h"",%c,%t,%a,%s";
                    contents.Add("Name,Path,Date Created,Date Modified,Last access date,Size");
                }

                contents.AddRange(DataModel.Format(format));

                try
                {
                    File.WriteAllLines(filename, contents);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Can not save file '" + filename + "'. An error occured: " + ex.Message, "",
                        MessageBoxButton.OK);
                }
            }
        }


        private void NewSearchWindowExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // Helper.OpenNewIndexerWnd(string.Empty);
        }

        private void NewSearchWindowCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }


        private void CloseSearchWindowCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void CloseSearchWindowExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }


        private void ExitAppCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void ExitAppExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Helper.ExitApplication();
        }


        private void DeleteCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // ToDo: implement

            //e.CanExecute = ListView.SelectedItems.Count > 0;
            e.Handled = true;
        }

        private void DeleteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // ToDo: implement

            //var fi = (FileInfoWrapper)ListView.SelectedItems[0];
            //if (!Directory.Exists(fi.Path)) return;
            //var menu = GetShellContextMenu(fi);
            //menu.InvokeDelete();
        }


        private void RenameExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // ToDo: implement

            //var fi = (FileInfoWrapper)ListView.SelectedItems[0];
            //if (!Directory.Exists(fi.Path)) return;
            //var menu = GetShellContextMenu(fi);
            //menu.InvokeRename();
        }

        private void RenameCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // ToDo: implement

            //e.CanExecute = ListView.SelectedItems.Count > 0;
            e.Handled = true;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UserSettings.Instance.Save(this);
        }

        public void Dispose()
        {
            Log.Instance.Debug("MainWnd Dispose called.");
            //DrivesManager.OnDriveSelectedChanged += DrivesManager_OnDriveSelectedChanged;
        }

        private void OnFilters_Click(object sender, RoutedEventArgs e)
        {
            FiltersVisibility = FiltersVisibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        }

        //private void OnMenuDetailsView_Click(object sender, RoutedEventArgs e)
        //{
        //    ViewType = ViewType.Details;

        //    IconsMenuITem.IsChecked = false;
        //    DetailsMenuITem.IsChecked = true;
        //}

        //private void OnMenuIconsView_Click(object sender, RoutedEventArgs e)
        //{
        //    ViewType = ViewType.Icons;

        //    IconsMenuITem.IsChecked = true;
        //    DetailsMenuITem.IsChecked = false;
        //}

        //private void OnMenuLargeIconsView_Click(object sender, RoutedEventArgs e)
        //{
        //    IconSize = IconSizeEnum.LargeIcon48;

        //    SmallIconsMenuItem.IsChecked = false;
        //    MediumIconsMenuItem.IsChecked = false;
        //    LargeIconsMenuItem.IsChecked = true;
        //    ExtraLargeIconsMenuItem.IsChecked = false;
        //}

        //private void OnMenuMediumIconsView_Click(object sender, RoutedEventArgs e)
        //{
        //    IconSize = IconSizeEnum.MediumIcon32;

        //    SmallIconsMenuItem.IsChecked = false;
        //    MediumIconsMenuItem.IsChecked = true;
        //    LargeIconsMenuItem.IsChecked = false;
        //    ExtraLargeIconsMenuItem.IsChecked = false;
        //}

        //private void OnMenuSmallIconsView_Click(object sender, RoutedEventArgs e)
        //{
        //    IconSize = IconSizeEnum.SmallIcon16;

        //    SmallIconsMenuItem.IsChecked = true;
        //    LargeIconsMenuItem.IsChecked = false;
        //    MediumIconsMenuItem.IsChecked = false;
        //    ExtraLargeIconsMenuItem.IsChecked = false;
        //}

        //private void OnMenuExtraLargeIconsView_Click(object sender, RoutedEventArgs e)
        //{
        //    IconSize = IconSizeEnum.JumboIcon256;

        //    SmallIconsMenuItem.IsChecked = false;
        //    MediumIconsMenuItem.IsChecked = false;
        //    LargeIconsMenuItem.IsChecked = false;
        //    ExtraLargeIconsMenuItem.IsChecked = true;
        //}

        //private void MainWindow_OnMouseWheel(object sender, MouseWheelEventArgs e)
        //{
        //    if (Keyboard.Modifiers == ModifierKeys.Control)
        //    {
        //        if (e.Delta > 0)
        //        {
        //            switch (IconSize)
        //            {
        //                case IconSizeEnum.SmallIcon16:
        //                    OnMenuMediumIconsView_Click(null, null);
        //                    break;
        //                case IconSizeEnum.MediumIcon32:
        //                    OnMenuLargeIconsView_Click(null, null);
        //                    break;
        //                case IconSizeEnum.LargeIcon48:
        //                    OnMenuExtraLargeIconsView_Click(null, null);
        //                    break;

        //                default:
        //                    throw new ArgumentOutOfRangeException();
        //            }
        //        }
        //        else
        //        {
        //            switch (IconSize)
        //            {
        //                case IconSizeEnum.MediumIcon32:
        //                    OnMenuSmallIconsView_Click(null, null);
        //                    break;
        //                case IconSizeEnum.LargeIcon48:
        //                    OnMenuMediumIconsView_Click(null, null);
        //                    break;
        //                case IconSizeEnum.JumboIcon256:
        //                    OnMenuLargeIconsView_Click(null, null);
        //                    break;
        //                default:
        //                    throw new ArgumentOutOfRangeException();
        //            }
        //        }

        //        e.Handled = true;
        //    }
        //}

        #region Context menu

        public void Rename(FrameworkElement frameworkElement)
        {
            var editableTextBlock = frameworkElement.FindChild<FileNameTextBlock>();

            if (editableTextBlock != null)
            {
                editableTextBlock.IsEditMode = true;
            }
        }

        private void ShowShellContextMenu(FileInfoWrapper item, FrameworkElement frameworkElement)
        {
            if (!SystemConfigFlagsWrapper.Instance().ShelContextMenu)
                return;

            var formsHost = (Control) ShellHostPopupWnd.FindName("ShellMenuHost");
            var pos = new Point((int) ShellHostPopupWnd.HorizontalOffset, (int) ShellHostPopupWnd.VerticalOffset);

            var path = item.Path;
            var fullName = item.FullName;

            var newItems = new List<ShellMenuItem>
            {
                new ShellMenuItem("Open Containing Folder in Explorer", 1,
                    () => {
                              Process.Start(path); // Opens the folder in Windows Explorer.
                    }),
                new ShellMenuItem("Copy Path to Clipboard", 2, () => Clipboard.SetText(path)),
                new ShellMenuItem("Copy Full Path to Clipboard", 3, () => Clipboard.SetText(item.FullName)),
                new ShellMenuItem("Copy Filename to Clipboard", 4, () => Clipboard.SetText(item.Name)),
                new ShellMenuItem("Rename", 5, () => Rename(frameworkElement))
            };

            var replaceItems = new List<ShellMenuItem> // Replace actions of items if needed.
            {
                new ShellMenuItem("Open", 0, () => OpenFileDefault(item.FullName))
            };

            ShellHostPopupWnd.IsOpen = true;
            var shelMenuManager = new ShellMenuManager(formsHost, fullName, pos, newItems, replaceItems);
            shelMenuManager.Show();
        }

        private static bool IsExecutable(string fullPath, out string filename)
        {
            var ext = Path.GetExtension(fullPath);
            if (ext == ".lnk")
            {
                fullPath = ShortcutResolver.Resolve(fullPath);
            }

            ext = Path.GetExtension(fullPath);
            filename = Path.GetFileName(fullPath);

            if (ext == ".exe" || ext == ".bat" || ext == ".cmd")
                return true;

            var twoBytes = new byte[2];
            try
            {
                using (var fileStream = File.Open(fullPath, FileMode.Open))
                {
                    fileStream.Read(twoBytes, 0, 2);
                }
            }
            catch
            {
            }

            return Encoding.UTF8.GetString(twoBytes) == "MZ";
        }

        private static void OpenFileDefault(string fullName)
        {
            string name;

            if (IsExecutable(fullName, out name))
            {
                var result =
                    MessageBox.Show(
                        "Do you want to allow the following program to make changes on this computer?\n\nProgramName:\t" +
                        name, "User Account Control", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            try
            {
                // Runs the default program for given file type or opens containing folder for dirs.
                Process.Start(fullName);
            }
            catch
            {
            }
        }


        public ICommand RowPreviewMouseRightButtonUp { get; private set; }

        public void DetailsViewRow_PreviewMouseUp(object sender)
        {
            var row = sender as ListViewItem;
            if (row == null || !row.IsSelected) return;

            var fi = row.Content as FileInfoWrapper;
            ShowShellContextMenu(fi, row);
        }


        public ICommand FileItemMouseDoubleClick { get; private set; }

        public void FileItem_MouseDoubleClick(object sender)
        {
            var row = sender as ListViewItem;
            if (row == null || !row.IsSelected) return;

            var fi = row.Content as FileInfoWrapper;

            if (fi != null)
            {
                OpenFileDefault(fi.FullName);
            }
        }

        #endregion
    }
}