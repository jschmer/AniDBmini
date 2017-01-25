﻿
#region Using Statements

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Forms = System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

using AniDBmini.Collections;
using AniDBmini.HashAlgorithms;

#endregion

namespace AniDBmini
{
    public partial class MainWindow : Window
    {

        #region Fields

        private const string AniDBaLink = @"http://anidb.net/perl-bin/animedb.pl?show=anime&aid=";

        public static string m_AppName = Application.ResourceAssembly.GetName().Name;

        private static string m_aLang;
        public static string animeLang
        {
            get { return m_aLang; }
        }

        private static string m_eLang;
        public static string episodeLang
        {
            get { return m_eLang; }
        }

        private int _pendingTasks;
        public int m_pendingTasks
        {
            get { return _pendingTasks; }
            set
            {
                _pendingTasks = value;
            }
        }

        private Forms.NotifyIcon m_notifyIcon;
        private Forms.ContextMenu m_notifyContextMenu = new Forms.ContextMenu();
        private WindowState m_storedWindowState = WindowState.Normal;

        private BackgroundWorker m_HashWorker;

        private AniDBAPI m_aniDBAPI;
        private MylistDB m_myList;

        private DateTime m_hashingStartTime;
        private Object m_hashingLock = new Object();

        private int m_storedTabIndex;
        private bool isHashing;
        private double totalQueueSize, ppSize;

        private string[] allowedVideoFiles = { "*.avi", "*.mkv", "*.mov", "*.mp4", "*.mpeg", "*.mpg", "*.ogm", "*.rm", "*.rmvb", "*.ts", "*.wmv" };

        private TSObservableCollection<MylistStat> mylistStatsList = new TSObservableCollection<MylistStat>();
        private TSObservableCollection<HashItem> hashFileList = new TSObservableCollection<HashItem>();
        private TSObservableCollection<AnimeTab> animeTabList = new TSObservableCollection<AnimeTab>();

        #endregion Fields

        #region Constructor

        public MainWindow(AniDBAPI api)
        {
            m_aniDBAPI = api;
            AniDBAPI.AppendApiDebugLine("Welcome to AniDBmini, connected to: " + m_aniDBAPI.APIServer);

            InitializeComponent();

            SetMylistVisibility();

            mylistStats.ItemsSource = mylistStatsList;
            apiDebugListBox.ItemsSource = m_aniDBAPI.ApiDebugLog;
            hashDebugListBox.ItemsSource = m_aniDBAPI.HashDebugLog;
            hashingListBox.ItemsSource = hashFileList;
            animeTabControl.ItemsSource = animeTabList;

            animeTabList.OnCountChanged += new CountChangedHandler(animeTabList_OnCountChanged);

            m_aniDBAPI.OnFileHashingProgress += new FileHashingProgressHandler(OnFileHashingProgress);
            m_aniDBAPI.OnAnimeTabFetched += new AnimeTabFetchedHandler(OnAnimeTabFetched);
            m_aniDBAPI.OnFileInfoFetched += new FileInfoFetchedHandler(OnFileInfoFetched);
        }

        #endregion Constructor

        #region Initialize

        /// <summary>
        /// Retrieves and formats mylist stats.
        /// </summary>
        private void InitializeStats()
        {
            int[] stats = m_aniDBAPI.MyListStats();

            for (int i = 0; i < stats.Length; i++)
            {
                string text = AniDBAPI.statsText[i], value;
                int stat = stats[i];

                if (text != "x")
                {
                    if (i == 3)
                        value = ((double)stat).ToFormattedBytes(ExtensionMethods.BYTE_UNIT.MB, ExtensionMethods.BYTE_UNIT.GB);
                    else if (i == 16)
                    {
                        int days = (int)Math.Floor((stat / 60f) / 24f);
                        int hours = (int)Math.Floor((((stat / 60f) / 24f) - (int)Math.Floor((stat / 60f) / 24f)) * 24);
                        int minutes = (int)((Math.Round((((stat / 60f) / 24f) - (int)Math.Floor((stat / 60f) / 24f)) * 24, 2) - hours) * 60);
                        value = days + "d " + hours + "h " + minutes + "m";
                    }
                    else if (i >= 10 && i <= 12)
                        value = stat + "%";
                    else
                        value = stat.ToString();

                    mylistStatsList.Add(new MylistStat(text, value));
                }
            }
        }

        /// <summary>
        /// Load current config file settings.
        /// </summary>
        private void InitializeConfig()
        {
            m_aLang = ConfigFile.Read("aLang").ToString();
            m_eLang = ConfigFile.Read("eLang").ToString();
        }

        /// <summary>
        /// Initializes the tray icon.
        /// </summary>
        private void InitializeNotifyIcon() // TODO: add options (minimize on close, disable tray icon, always show, etc.)
        {
            m_notifyIcon = new Forms.NotifyIcon();
            m_notifyIcon.Text = this.Title;
            m_notifyIcon.Icon = new System.Drawing.Icon(global::AniDBmini.Properties.Resources.AniDBmini, 16, 16);
            m_notifyIcon.MouseDoubleClick += (s, e) => { this.Show(); WindowState = m_storedWindowState; };
            m_notifyIcon.ContextMenu = m_notifyContextMenu;

            Forms.MenuItem cm_open = new Forms.MenuItem();
            cm_open.Text = "Open";
            cm_open.Click += (s, e) => { this.Show(); WindowState = m_storedWindowState; };
            m_notifyContextMenu.MenuItems.Add(cm_open);

            m_notifyContextMenu.MenuItems.Add("-");

            Forms.MenuItem cm_exit = new Forms.MenuItem();
            cm_exit.Text = "Exit";
            cm_exit.Click += (s, e) => { this.Close(); };
            m_notifyContextMenu.MenuItems.Add(cm_exit);
        }

        #endregion

        #region Private Methods

        #region Hashing

        /// <summary>
        /// Creates a entry and adds it to the hash list.
        /// </summary>
        /// <param name="path">Path to file.</param>
        private void addRowToHashTable(string path)
        {
            HashItem item = new HashItem(path);
            hashFileList.Add(item);

            if (isHashing)
                totalQueueSize += item.Size;
            else if (!isHashing)
                Dispatcher.BeginInvoke(new Action(delegate { hashingStartButton.IsEnabled = true; }));
        }

        /// <summary>
        /// Adds a hashItem to the hash list.
        /// </summary>
        /// <param name="path">Path to file.</param>
        private void addRowToHashTable(HashItem item)
        {
            hashFileList.Insert(0, item);

            if (isHashing)
                totalQueueSize += item.Size;
            else if (!isHashing)
                beginHashing();
        }

        /// <summary>
        /// Removes a hash entry from the list.
        /// </summary>
        /// <param name="item">Item to remove.</param>
        /// <param name="userRemoved">True if removed by user.</param>
        private void removeRowFromHashTable(HashItem item, bool userRemoved = false)
        {
            if (isHashing && userRemoved)
            {
                totalQueueSize -= item.Size;

                if (item == hashFileList[0])
                    m_aniDBAPI.cancelHashing();
            }

            lock (m_hashingLock)
                hashFileList.Remove(item);

            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (hashFileList.Count == 0)
                    hashingStartButton.IsEnabled = hashingStopButton.IsEnabled = false;
            }));
        }

        /// <summary>
        /// Initializes the hashing background worker.
        /// </summary>
        private void beginHashing()
        {
            hashingStartButton.IsEnabled = false;
            hashingStopButton.IsEnabled = isHashing = true;

            totalQueueSize = 0;

            for (int i = 0; i < hashFileList.Count; i++)
                totalQueueSize += hashFileList[i].Size;

            m_HashWorker = new BackgroundWorker();
            m_HashWorker.WorkerSupportsCancellation = true;

            m_HashWorker.DoWork += new DoWorkEventHandler(OnHashWorkerDoWork);
            m_HashWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnHashWorkerCompleted);

            m_hashingStartTime = DateTime.Now;
            m_HashWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Adds completed hash item to mylist.
        /// </summary>
        private void FinishHash(HashItem item)
        {
            ppSize += item.Size;

            // does item already exist in database?
            FileEntry file = m_myList.SelectFile(item.Hash, item.Path);

            if (addToMyListCheckBox.IsChecked == true)
            {
                item.Watched = (bool)watchedCheckBox.IsChecked;
                item.State = stateComboBox.SelectedIndex;

                EpisodeEntry epEntry = m_myList.SelectEpisodeForFile(file);

                if ( epEntry.eid != 0 && 
                    ( epEntry.watched != item.Watched
                      || file.state != item.State) )
                {
                    // Episode info already exists in database, update MyList data
                    item.Edit = true;
                    m_aniDBAPI.MyListAdd(item);
                }
                else if ( epEntry.eid != 0 )
                {
                    // Episode info already exist and no changes needed
                    AniDBAPI.AppendHashDebugLine(String.Format("Episode entry already exists, skipping: {0}", file.path));
                }
                else
                {
                    // Episode info does not exist, add to MyList
                    m_aniDBAPI.MyListAdd(item);
                }

            }
            else if (file.fid != 0)
            {
                // File already exists in database -> done
                AniDBAPI.AppendHashDebugLine(String.Format("File entry already exists, skipping: {0}", file.path));
            }
            else
            {
                m_aniDBAPI.GetFileData(item);
            }

            
        }

        #endregion Hashing

        #region Mylist

        /// <summary>
        /// Sets mylist visibility based on sql connection.
        /// </summary>
        private void SetMylistVisibility()
        {
            if (m_myList.isSQLConnOpen)
            {
                MylistImortButton.Visibility = Visibility.Collapsed;
                MylistTreeListView.IsEnabled = true;
            }
            else
            {
                MylistImortButton.Visibility = Visibility.Visible;
                MylistTreeListView.IsEnabled = false;
            }
        }

        private bool SetFileLocation(FileEntry entry)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Video Files|" + String.Join(";", allowedVideoFiles) + "|All Files|*.*";
            dlg.Title = String.Format("Browsing for {0} Episode {1}", m_myList.SelectAnimeFromFile(entry.fid).title, entry.epno);

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                entry.path = dlg.FileName;
                MylistTreeListView.Refresh();

                return true;
            }

            return false;
        }

        private List<string> GetFilePathList(object sender)
        {
            object entry = ((MenuItem)sender).Tag;
            List<string> fPaths = new List<string>();

            if (entry is AnimeEntry)
            {
                foreach (FileEntry file in m_myList.SelectFilesFromAnime(((AnimeEntry)entry).aid))
                    if (File.Exists(file.path))
                        fPaths.Add(file.path);
            }
            else if (entry is FileEntry)
            {
                FileEntry fEntry = (FileEntry)entry;

                if (File.Exists(fEntry.path))
                    fPaths.Add(fEntry.path);
                else if (MessageBox.Show("Would you like to locate the file?", "File not found!",
                    MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes &&
                    SetFileLocation(fEntry))
                { }
            }

            return fPaths;
        }

        #endregion Mylist

        #endregion Private Methods

        #region Events

        #region Main Window

        private void OnInitialized(object sender, EventArgs e)
        {
            InitializeStats();
            InitializeConfig();
            InitializeNotifyIcon();

            m_myList = new MylistDB();

            if (m_myList.isSQLConnOpen)
                MylistTreeListView.Model = new MylistModel(m_myList);
        }

        private void ShowOpionsWindow(object sender, RoutedEventArgs e)
        {
            OptionsWindow options = new OptionsWindow();
            options.Owner = this;

            options.ShowDialog();
        }

        private void randomAnimeButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            ContextMenu cm = btn.ContextMenu;
            cm.PlacementTarget = btn;
            cm.IsOpen = true;
        }

        private void randomAnimeLabelContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            m_aniDBAPI.RandomAnime(int.Parse(mi.Tag.ToString()));
        }

        private void OnAnimeTabFetched(AnimeTab aTab)
        {
            animeTabList.Add(aTab);
        }

        private void OnFileInfoFetched(FileInfoFetchedArgs e)
        {
            if (!m_myList.isSQLConnOpen)
            {
                Dispatcher.Invoke(new Action(m_myList.Create));
                Dispatcher.BeginInvoke(new Action(SetMylistVisibility));
            }

            m_myList.InsertFileInfo(e);
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (MylistTreeListView != null)
                    MylistTreeListView.Refresh();
            }));
        }

        private void mainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((TabItem)mainTabControl.SelectedItem != animeTabItem)
                m_storedTabIndex = mainTabControl.SelectedIndex;
        }

        private void OnStateChanged(object sender, EventArgs args)
        {
            if (this.WindowState == WindowState.Minimized)
                this.Hide();
            else
                m_storedWindowState = this.WindowState;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = !this.IsVisible;
        }

        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            m_myList.Close();
            m_aniDBAPI.Logout();

            m_notifyIcon.Dispose();
            m_notifyIcon = null;
        }

        #endregion Main Window

        #region Home Tab

        private void refreshStatsButton_Click(object sender, RoutedEventArgs e)
        {
            mylistStatsList.Clear();
            InitializeStats();

            Button btn = (Button)sender;
            btn.IsEnabled = false;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(60);
            timer.Tick += delegate
            {
                btn.IsEnabled = true;
                timer.Stop();
            };
            timer.Start();
        }

        private void clearApiDebugLog(object sender, RoutedEventArgs e)
        {
            m_aniDBAPI.ApiDebugLog.Clear();
        }

        private void clearHashDebugLog(object sender, RoutedEventArgs e)
        {
            m_aniDBAPI.HashDebugLog.Clear();
        }

        private void apiDebugListBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (apiDebugListBoxScrollViewer.VerticalOffset == apiDebugListBoxScrollViewer.ScrollableHeight)
                apiDebugListBoxScrollViewer.ScrollToBottom();
        }

        private void hashDebugListBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (hashDebugListBoxScrollViewer.VerticalOffset == hashDebugListBoxScrollViewer.ScrollableHeight)
                hashDebugListBoxScrollViewer.ScrollToBottom();
        }

        #endregion Home Tab

        #region Hashing Tab

        static List<string> ListFiles(string sDir)
        {
            List<string> files = new List<string>();
            foreach (string f in Directory.GetFiles(sDir))
            {
                FileInfo fi = new FileInfo(f);
                if (!fi.Attributes.HasFlag(FileAttributes.Directory))
                    files.Add(f);
            }
            foreach (string d in Directory.GetDirectories(sDir))
            {
                files.AddRange(ListFiles(d));
            }
            return files;
        }

        private void hashingListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                Array.Sort(paths);

                foreach (var path in paths)
                {
                    List<string> all_files = new List<string>();
                    FileInfo fi = new FileInfo(path);
                    if (fi.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        all_files.AddRange(ListFiles(path));
                    }
                    else
                    {
                        all_files.Add(path);
                    }

                    foreach (var file in all_files)
                    {
                        fi = new FileInfo(file);
                        if (allowedVideoFiles.Contains<string>("*" + fi.Extension.ToLower()))
                            lock (m_hashingLock)
                                addRowToHashTable(fi.FullName);
                    }
                }
            }
        }

        private void removeSelectedHashItems(object sender, RoutedEventArgs e)
        {
            while (hashingListBox.SelectedItems.Count > 0)
                removeRowFromHashTable(hashingListBox.SelectedItems[0] as HashItem, true);
        }

        private void clearHashItems(object sender, RoutedEventArgs e)
        {
            if (isHashing)
                hashingStopButton_Click(this, null);

            hashFileList.Clear();
        }

        private void hashingListBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
                removeSelectedHashItems(sender, null);
        }

        private void startHashingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isHashing)
                beginHashing();
        }

        private void hashingStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isHashing)
            {
                m_HashWorker.CancelAsync();
                m_aniDBAPI.cancelHashing();

                OnHashWorkerCompleted(sender, null);
            }
        }

        private void OnHashWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            while (hashFileList.Count > 0 && isHashing)
            {
                if (m_HashWorker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                HashItem thisItem = hashFileList[0],
                        _temp = m_aniDBAPI.ed2kHash(thisItem);

                if (isHashing && _temp != null) // if we did not abort remove item from queue and process
                {
                    Dispatcher.BeginInvoke(new Action<HashItem>(FinishHash), _temp);
                    removeRowFromHashTable(hashFileList[hashFileList.IndexOf(thisItem)]);
                }
            }
        }

        private void OnHashWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            hashingStopButton.IsEnabled = isHashing = false;
            fileProgressBar.Value = totalProgressBar.Value = ppSize = 0;
            hashingStartButton.IsEnabled = hashFileList.Count > 0;
            timeRemainingTextBlock.Text = timeElapsedTextBlock.Text = totalBytesTextBlock.Text = String.Empty;

            m_HashWorker.Dispose();
        }

        private void addFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Video Files|" + String.Join(";", allowedVideoFiles) + "|All Files|*.*";
            dlg.Multiselect = true;

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate
                {
                    lock (m_hashingLock)
                        for (int i = 0; i < dlg.FileNames.Length; i++)
                            addRowToHashTable(dlg.FileNames[i]);
                }));
            }
        }

        private void addFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            Forms.FolderBrowserDialog dlg = new Forms.FolderBrowserDialog();
            dlg.ShowNewFolderButton = false;

            Forms.DialogResult result = dlg.ShowDialog();
            if (result == Forms.DialogResult.OK)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate
                {
                    lock (m_hashingLock)
                    {
                        foreach (string _file in Directory.GetFiles(dlg.SelectedPath, "*.*")
                            .Where(x => allowedVideoFiles.Contains("*" + Path.GetExtension(x).ToLower())))
                            addRowToHashTable(_file);

                        foreach (string dir in Directory.GetDirectories(dlg.SelectedPath))
                            try
                            {
                                foreach (string _file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                                    .Where(x => allowedVideoFiles.Contains("*" + Path.GetExtension(x).ToLower())))
                                    addRowToHashTable(_file);
                            }
                            catch (UnauthorizedAccessException) { }
                    }
                }));
            }
        }

        private void OnFileHashingProgress(object sender, FileHashingProgressArgs e)
        {
            double fileProg = e.ProcessedSize / e.TotalSize * 100;
            double totalProg = (e.ProcessedSize + ppSize) / totalQueueSize * 100;

            TimeSpan totalTimeElapsed = DateTime.Now - m_hashingStartTime;
            TimeSpan remainingSpan = TimeSpan.FromSeconds(Math.Ceiling(totalQueueSize * (totalTimeElapsed.TotalSeconds / (ppSize + e.ProcessedSize)) - totalTimeElapsed.TotalSeconds));

            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (isHashing)
                {
                    timeElapsedTextBlock.Text = String.Format("Elapsed: {0}", totalTimeElapsed.ToHMS());
                    timeRemainingTextBlock.Text = String.Format("ETA: {0}", remainingSpan.ToHMS());
                    totalBytesTextBlock.Text = String.Format("Bytes: {0} / {1}", (e.ProcessedSize + ppSize).ToFormattedBytes(ExtensionMethods.BYTE_UNIT.GB),
                                                                                 totalQueueSize.ToFormattedBytes(ExtensionMethods.BYTE_UNIT.GB));

                    fileProgressBar.Value = fileProg;
                    totalProgressBar.Value = totalProg;
                }
            }));
        }

        #endregion Hashing Tab

        #region Mylist Tab

        private void ImportList_Click(object sender, RoutedEventArgs e)
        {
            ImportWindow import = new ImportWindow(m_myList);
            import.Owner = this;

            MylistImortButton.Visibility = Visibility.Collapsed;

            import.ShowDialog();
            SetMylistVisibility();
        }

        private void OpenADBPage_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(AniDBaLink + (sender as MenuItem).Tag.ToString());
        }
        
        private void EntryViewDetails_Click(object sender, RoutedEventArgs e)
        {
            m_aniDBAPI.Anime(int.Parse((sender as MenuItem).Tag.ToString()));
        }

        private void FetchEntryInfo_Click(object sender, RoutedEventArgs e)
        {
            object entry = (sender as MenuItem).Tag;

            if (entry is AnimeEntry)
            {
                FileEntry fEntry = m_myList.SelectFileFromAnime((entry as AnimeEntry).aid);
                if (fEntry != null)
                    m_aniDBAPI.GetFileData(fEntry);
            }
            else if (entry is FileEntry)
                m_aniDBAPI.GetFileData(entry as FileEntry);
        }

        /// <summary>
        /// This is not completely foolproof,
        /// but it works most of the time.
        /// NOT recursive. Will NOT add OP's ED's or Specials.
        /// </summary>
        private void LocateFiles_Click(object sender, RoutedEventArgs e)
        {
            object entry = (sender as MenuItem).Tag;

            if (entry is AnimeEntry)
            {
                List<FileEntry> files = m_myList.SelectFilesFromAnime((entry as AnimeEntry).aid);

                if (files.Count > 0)
                {
                    Forms.FolderBrowserDialog dlg = new Forms.FolderBrowserDialog();
                    dlg.ShowNewFolderButton = false;

                    Forms.DialogResult result = dlg.ShowDialog();
                    if (result == Forms.DialogResult.OK)
                    {
                        ThreadPool.QueueUserWorkItem(new WaitCallback(delegate
                        {
                                List<string> fPaths = new List<string>();
                                List<string> uPaths = new List<string>();

                                foreach (string _file in Directory.GetFiles(dlg.SelectedPath, "*.*")
                                    .Where(x => allowedVideoFiles.Contains("*" + Path.GetExtension(x).ToLower())))
                                    fPaths.Add(_file);

                                foreach (FileEntry file in files)
                                {
                                    string path = fPaths.FirstOrDefault(x => !uPaths.Contains(x) &&
                                        Regex.IsMatch(x, String.Format(@"([p\.\[\]\(\) _-]0?{0}[v\.\[\]\(\) _-]).*(\[[A-F0-9]{{8}}])?", file.epno)));
                                    if (path != string.Empty)
                                    {
                                        file.path = path;
                                        uPaths.Add(path);
                                    }
                                }
                        }));
                    }

                    MylistTreeListView.Refresh();
                }
                else
                    MessageBox.Show("No file entries found for the selected anime.");
            }
            else if (entry is FileEntry)
                SetFileLocation(entry as FileEntry);
        }

        // TODO
        private void MarkWatched_Click(object sender, RoutedEventArgs e)
        {

        }

        // TODO
        private void MarkUnwatched_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RemoveFromList_Click(object sender, RoutedEventArgs e)
        {
            FileEntry fEntry = (FileEntry)(sender as MenuItem).Tag;

            if (m_aniDBAPI.MyListDel(fEntry.lid))
            {
                if (!String.IsNullOrEmpty(fEntry.path))
                    AniDBAPI.AppendApiDebugLine(String.Format("Removed {0} from mylist.", Path.GetFileName(fEntry.path)));

                m_myList.DeleteFile(fEntry.lid);
                MylistTreeListView.Refresh();
            }
        }

        #endregion Mylist Tab

        #region Anime Tab

        private void OnTabCloseClick(object sender, RoutedEventArgs e)
        {
            Button s = sender as Button;
            animeTabList.RemoveAll(x => x.AnimeID == int.Parse(s.Tag.ToString()));
        }

        private void animeTabList_OnCountChanged(object sender, CountChangedArgs e)
        {
            if (e.oldCount == 1 && e.newCount == 0) // no more tabs
            {
                animeTabItem.Visibility = System.Windows.Visibility.Collapsed;
                mainTabControl.SelectedIndex = m_storedTabIndex;
            }
            else
            {
                if (e.oldCount == 0 && e.newCount == 1) // first tab
                {
                    animeTabItem.Visibility = System.Windows.Visibility.Visible;
                    m_storedTabIndex = mainTabControl.SelectedIndex;
                }

                animeTabItem.Focus();
                animeTabControl.SelectedIndex = e.newCount - 1;
            }
        }

        private void AnimeURLClick(object sender, RoutedEventArgs e)
        {
            Image s_img = (Image)sender;
            System.Diagnostics.Process.Start(s_img.Tag.ToString());
        }

        #endregion Anime Tab

        #endregion

        #region Properties

        public string wTitle
        {
            get { return this.Title; }
            set
            {
                this.Title = value;
                m_notifyIcon.Text = value.Truncate(63, false, true);
            }
        }

        #endregion Properties

    }
}