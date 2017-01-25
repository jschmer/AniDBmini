﻿
#region Using Statements

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

using AniDBmini.Collections;
using AniDBmini.HashAlgorithms;

#endregion

namespace AniDBmini
{
    public class QueuedAniDBAPI
    {
        #region Fields

        private AniDBAPI anidbAPI = null;

        private ConcurrentQueue<Action> apiCallQueue = new ConcurrentQueue<Action>();
        private Thread apiCallWorker = null;
        private AutoResetEvent stopEvent = new AutoResetEvent(false);

        public event FileInfoFetchedHandler OnFileInfoFetched = delegate { };
        public event AnimeTabFetchedHandler OnAnimeTabFetched = delegate { };
        public event FileHashingProgressHandler OnFileHashingProgress = delegate { };

        public bool Connected { get { return anidbAPI.isConnected; } }

        #endregion Fields

        #region Constructor

        public QueuedAniDBAPI(string server, int port, int localPort)
        {
            // Create wrapped API object
            anidbAPI = new AniDBAPI(server, port, localPort);

            // Wire up events
            anidbAPI.OnFileInfoFetched += delegate(FileInfoFetchedArgs args)
            {
                OnFileInfoFetched(args);
            };
            anidbAPI.OnAnimeTabFetched += delegate (AnimeTab tab)
            {
                OnAnimeTabFetched(tab);
            };
            anidbAPI.OnFileHashingProgress += delegate (object sender, FileHashingProgressArgs args)
            {
                OnFileHashingProgress(sender, args);
            };
        }

        #endregion Constructor

        #region APICALL_WORKER
        private void StartAPICallWorker()
        {
            StopAPICallWorker();

            stopEvent.Reset();
            apiCallWorker = new Thread(ProcessAPICalls);
            apiCallWorker.Start();
        }

        private void StopAPICallWorker()
        {
            if (apiCallWorker != null)
            {
                stopEvent.Set();
                apiCallWorker.Join();
            }
            apiCallWorker = null;
        }

        private void ProcessAPICalls()
        {
            while (!stopEvent.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Action action;
                if (apiCallQueue.TryDequeue(out action))
                {
                    action();
                }
            }

        }
        #endregion

        #region ANIDB_API
        #region AUTH
        public bool Login(string user, string pass)
        {
            bool loggedIn = anidbAPI.Login(user, pass);
            if (loggedIn)
            {
                StartAPICallWorker();
            }

            return loggedIn;
        }

        public void Logout()
        {
            anidbAPI.Logout();

            StopAPICallWorker();
        }

        #endregion AUTH

        #region DATA

        /// <summary>
        /// Anime command to create a anime tab from an anime ID.
        /// </summary>
        public void Anime(int animeID)
        {
            Action animeTab = new Action(delegate
            {
                anidbAPI.Anime(animeID);
            });

            QueueAPICommand(animeTab);
        }

        public void GetFileData(HashItem item)
        {
            Action fileInfo = new Action(delegate
            {
                anidbAPI.GetFileData(item);
            });

            QueueAPICommand(fileInfo);
        }

        public void GetFileData(FileEntry entry)
        {
            Action fileInfo = new Action(delegate
            {
                anidbAPI.GetFileData(entry);
            });

            QueueAPICommand(fileInfo);
        }

        #endregion DATA

        #region MYLIST

        public void MyListAdd(HashItem item)
        {
            Action addToList = new Action(delegate
            {
                anidbAPI.MyListAdd(item);
            });

            QueueAPICommand(addToList);
        }

        public bool MyListDel(int lid)
        {
            return anidbAPI.MyListDel(lid);
        }

        /// <summary>
        /// Retrieves mylist stats.
        /// </summary>
        /// <returns>Array of stat values.</returns>
        public int[] MyListStats()
        {
            return anidbAPI.MyListStats();
        }

        /// <summary>
        /// Retrieves a random anime from a certian criteria.
        /// </summary>
        /// <param name="type">type: 0=from db, 1=watched, 2=unwatched, 3=all mylist</param>
        public void RandomAnime(int type)
        {
            Action random = new Action(delegate
            {
                anidbAPI.RandomAnime(type);
            });

            QueueAPICommand(random);
        }

        #endregion MYLIST

        #region File Hashing

        public HashItem ed2kHash(HashItem item)
        {
            return anidbAPI.ed2kHash(item);
        }

        public void cancelHashing()
        {
            anidbAPI.cancelHashing();
        }

        #endregion File Hashing
        #endregion

        #region Private Methods

        private void QueueAPICommand(Action command)
        {
            anidbAPI.MainWindow.PendingTasks++;

            apiCallQueue.Enqueue(new Action(delegate
            {
                command();

                anidbAPI.MainWindow.PendingTasks--;
            }));
        }

        #endregion Private Methods

        #region Properties & Static Methods

        public MainWindow MainWindow
        {
            set { anidbAPI.MainWindow = value; }
        }

        public IPEndPoint APIServer { get { return anidbAPI.APIServer; } }
        public TSObservableCollection<DebugLine> ApiDebugLog { get { return anidbAPI.ApiDebugLog; } }
        public TSObservableCollection<DebugLine> HashDebugLog { get { return anidbAPI.HashDebugLog; } }

        #endregion Properties

    }
}