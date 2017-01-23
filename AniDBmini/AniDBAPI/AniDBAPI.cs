﻿
#region Using Statements

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Diagnostics;
using System.Windows.Threading;

using AniDBmini.Collections;
using AniDBmini.HashAlgorithms;

#endregion

namespace AniDBmini
{

    #region Args & Delegates

    public class FileInfoFetchedArgs : EventArgs
    {
        public AnimeEntry Anime { get; private set; }
        public EpisodeEntry Episode { get; private set; }
        public FileEntry File { get; private set; }

        public FileInfoFetchedArgs(AnimeEntry anime, EpisodeEntry episode, FileEntry file)
        {
            Anime = anime;
            Episode = episode;
            File = file;
        }
    }

    public delegate void FileInfoFetchedHandler(FileInfoFetchedArgs fInfo);
    public delegate void AnimeTabFetchedHandler(AnimeTab aTab);

    #endregion Args & Delegates

    public class AniDBAPI
    {
        #region Enums

        private enum RETURN_CODE
        {
            LOGIN_ACCEPTED                           = 200,
            LOGIN_ACCEPTED_NEW_VERSION               = 201,
            MYLIST_ENTRY_ADDED                       = 210,
            MYLIST_ENTRY_DELETED                     = 211,
            FILE                                     = 220,
            MYLIST_STATS                             = 222,
            ANIME                                    = 230,
            FILE_ALREADY_IN_MYLIST                   = 310,
            MYLIST_ENTRY_EDITED                      = 311,
            NO_SUCH_FILE                             = 320,
            NO_SUCH_ENTRY                            = 321,
            NO_SUCH_ANIME                            = 330,
            NO_SUCH_GROUP                            = 350,
            NO_SUCH_MYLIST_ENTRY                     = 411,
            LOGIN_FAILED                             = 500,
            LOGIN_FIRST                              = 501,
            ACCESS_DENIED                            = 502,
            CLIENT_VERSION_OUTDATED                  = 503,
            CLIENT_BANNED                            = 504,
            ILLEGAL_INPUT_OR_ACCESS_DENIED           = 505,
            INVALID_SESSION                          = 506,
            INTERNAL_SERVER_ERROR                    = 600,
            ANIDB_OUT_OF_SERVICE                     = 601,
            SERVER_BUSY                              = 602
        };

        public enum REMOVE_TYPE
        {
            LID,
            FID,
            AID
        };

        #endregion Enums

        #region Structs

        private struct APIResponse
        {
            public string Message;
            public RETURN_CODE Code;
        }

        #endregion Structs

        #region Mock Stuff
#if MOCK_REMOTE_API
        private static APIResponse CreateResponse(RETURN_CODE code, string DataShort, string DataDetailed = "")
        {
            return new APIResponse { Message = String.Format("{0} {1}\n{2}{3}", code.ToString(), DataShort, DataDetailed, (DataDetailed.Length > 0 ? "\n" : "")), Code = code };
        }

        private Dictionary<string, APIResponse> mocked_api_responses = new Dictionary<string, APIResponse>
        {
            { "AUTH", CreateResponse(RETURN_CODE.LOGIN_ACCEPTED, "ABCDE LOGIN ACCEPTED") },
            { "MYLISTSTATS", CreateResponse(RETURN_CODE.MYLIST_STATS, "MYLIST STATS", "281|6406|6772|1406764|0|0|0|0|100|0|4|4|99|6326|0|0|150395") },
        };
#endif
    #endregion

    #region Fields

        private MainWindow mainWindow;
		private Ed2k hasher = new Ed2k();

        private UdpClient conn = new UdpClient();
        private IPEndPoint apiserver;
        private DateTime m_lastCommand;

        private Object queueLock = new Object();
        private List<DateTime> queryLog = new List<DateTime>();

        private byte[] data = new byte[1400];
        private bool isLoggedIn;
        private string sessionKey, user, pass;

        private static TSObservableCollection<DebugLine> debugLog = new TSObservableCollection<DebugLine>();
        
        public static string[] statsText = { "Anime",
                                             "Episodes",
                                             "Files",
                                             "Size",
                                             "x", "x", "x", "x", "x", "x",
                                             "AniDB watched",
                                             "AniDB in mylist",
                                             "Mylist watched",
                                             "Episodes watched",
                                             "x", "x",
                                             "Time wasted" };
        public bool isConnected;

        public event FileInfoFetchedHandler OnFileInfoFetched = delegate { };
        public event AnimeTabFetchedHandler OnAnimeTabFetched = delegate { };

		#endregion Fields

		#region Constructor

		public AniDBAPI(string server, int port, int localPort)
        {
            apiserver = new IPEndPoint(IPAddress.Any, localPort);

            while (!isConnected)
            {
                try
                {
#if !MOCK_REMOTE_API
                    conn.Connect(server, port);
#endif
                    isConnected = true;
                }
                catch (SocketException)
                {
                    MessageBoxResult res = MessageBox.Show(
                        "Unable to connect to api server!\nWould you like to try again?",
                        "Connection Error!",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error
                    );

                    if (res == MessageBoxResult.No)
                        break;
                }
            }
        }

        #endregion Constructor

        #region AUTH

        public bool Login(string user, string pass)
        {
            APIResponse response = Execute("AUTH user=" + user + "&pass=" + pass + "&protover=3&client=anidbmini&clientver=1&enc=UTF8");

            if (response.Code == RETURN_CODE.LOGIN_ACCEPTED || response.Code == RETURN_CODE.LOGIN_ACCEPTED_NEW_VERSION)
            {
                sessionKey = response.Message.Split(' ')[1];

                isLoggedIn = true;
                this.user = user;
                this.pass = pass;
            }
            else
            {
                MessageBox.Show(response.Message, "Login Failed!", MessageBoxButton.OK, MessageBoxImage.Error);
                isLoggedIn = false;
            }

            return isLoggedIn;
        }

        public void Logout()
        {
            Execute("LOGOUT");
        }

#endregion AUTH

#region DATA

        /// <summary>
        /// Anime command to create a anime tab from an anime ID.
        /// </summary>
        public void Anime(int animeID)
        {
            APIResponse response = Execute(String.Format("ANIME aid={0}&amask=B2E05EFE400080", animeID));

            if (response.Code == RETURN_CODE.ANIME)
            {
                AnimeTab aTab = (AnimeTab)mainWindow.Dispatcher.Invoke(new Func<AnimeTab>(delegate { return new AnimeTab(Regex.Split(response.Message, "\n")[1]); }));
                OnAnimeTabFetched(aTab);
            }
        }

        public void File(HashItem item)
        {
            Action fileInfo = new Action(delegate
            {
                APIResponse response = Execute(String.Format("FILE size={0}&ed2k={1}&fmask=78006A28B0&amask=F0E0F0C0", item.Size, item.Hash));

                if (response.Code == RETURN_CODE.FILE)
                    ParseFileData(item, response.Message);
            });

            PrioritizedCommand(fileInfo);
        }

        public void File(FileEntry entry)
        {
            Action fileInfo = new Action(delegate
            {
                APIResponse response = Execute(String.Format("FILE fid={0}&fmask=78006A28B0&amask=F0E0F0C0", entry.fid));

                if (response.Code == RETURN_CODE.FILE)
                    ParseFileData(entry, response.Message);
            });

            PrioritizedCommand(fileInfo);
        }

#endregion DATA

#region MYLIST

        public void MyListAdd(HashItem item)
        {
            Action addToList = new Action(delegate
            {
                string r_msg = String.Empty;
                APIResponse response = Execute(String.Format("MYLISTADD size={0}&ed2k={1}&viewed={2}&state={3}&edit={4}",
                    item.Size, item.Hash, Convert.ToInt32(item.Watched), item.State, Convert.ToInt32(item.Edit)));

                switch (response.Code)
                {
                    case RETURN_CODE.MYLIST_ENTRY_ADDED:
                        File(item);
                        r_msg = String.Format("Added {0} to mylist", item.Name);
                        break;
                    case RETURN_CODE.MYLIST_ENTRY_EDITED:
                        File(item);
                        r_msg = String.Format("Edited mylist entry for {0}", item.Name);
                        break;
                    case RETURN_CODE.FILE_ALREADY_IN_MYLIST: // TODO: add auto edit to options.
                        item.Edit = true;
                        MyListAdd(item);
                        return;
                    case RETURN_CODE.NO_SUCH_FILE:
                        r_msg = "Error! File not in database";
                        break;
                }

                AppendDebugLine(r_msg);
            });

            PrioritizedCommand(addToList);
        }

        public bool MyListDel(int lid)
        {
            return Execute(String.Format("MYLISTDEL lid={0}", lid)).Code == RETURN_CODE.MYLIST_ENTRY_DELETED;
        }

        /// <summary>
        /// Retrieves mylist stats.
        /// </summary>
        /// <returns>Array of stat values.</returns>
        public int[] MyListStats()
        {
            string r_msg = Execute("MYLISTSTATS").Message;
            return Array.ConvertAll<string, int>(Regex.Split(r_msg, "\n")[1].Split('|'), delegate(string s) { return int.Parse(s); });
        }

        /// <summary>
        /// Retrieves a random anime from a certian criteria.
        /// </summary>
        /// <param name="type">type: 0=from db, 1=watched, 2=unwatched, 3=all mylist</param>
        public void RandomAnime(int type)
        {
            Action random = new Action(delegate
            {
                APIResponse response = Execute(String.Format("RANDOMANIME type={0}", type));
                if (response.Code == RETURN_CODE.ANIME)
                {
                    Action anime = new Action(delegate { Anime(int.Parse(Regex.Split(response.Message, "\n")[1].Split('|')[0])); });
                    PrioritizedCommand(anime);
                }
            });

            PrioritizedCommand(random);
        }

#endregion MYLIST

#region File Hashing

        public HashItem ed2kHash(HashItem item)
        {
            hasher.Clear();
            FileInfo file = new FileInfo(item.Path);

            using (FileStream fs = file.OpenRead())
            {
                AppendDebugLine("Hashing " + item.Name);
                byte[] temp;

                if ((temp = hasher.ComputeHash(fs)) != null)
                {
                    item.Hash = string.Concat(temp.Select(b => b.ToString("x2")).ToArray());
                    AppendDebugLine("Ed2k hash: " + item.Hash);

                    return item;
                }
                else
                    AppendDebugLine("Hashing aborted");

                return null;
            }
        }

        public void cancelHashing()
        {
            hasher.Cancel();
            hasher.Clear();
        }

#endregion File Hashing

#region Private Methods

        /// <summary>
        /// Executes an action after a certain amount of time has passed
        /// since the previous command was sent to the server.
        /// </summary>
        /// <param name="todo">Action that will be executed after waiting.</param>
        private void PrioritizedCommand(Action Command)
        {
            ++mainWindow.m_pendingTasks;
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate
            {
                lock (queueLock)
                {
                    double secondsSince = DateTime.Now.Subtract(m_lastCommand).TotalSeconds;
                    int timeout = CalculateTimeout();

                    if (secondsSince < timeout)
                        Thread.Sleep(TimeSpan.FromSeconds(timeout - secondsSince));

                    Command();
                    --mainWindow.m_pendingTasks;                    
                }
            }));
        }

        /// <summary>
        /// Calculates the timeout for the next low priority command
        /// using a list of datetimes for every query in the past minute.
        /// </summary>
        /// <returns>Timeout in seconds.</returns>
        private int CalculateTimeout()
        {
            queryLog.RemoveAll(x => DateTime.Now.Subtract(x).TotalSeconds > 60); // remove old timestamps

            // A Client MUST NOT send more than 0.5 packets per second (that's one packet every two seconds, not two packets a second!)
            // A Client MUST NOT send more than one packet every four seconds over an extended amount of time.
            if (queryLog.Count < 10)
                return 2;
            else if (queryLog.Count < 15)
                return 3;
            else
                return 4;
        }

        /// <summary>
        /// Parses the return message of a FILE command,
        /// and then triggers the OnFileInfoFetched event.
        /// </summary>
        private void ParseFileData(object item, string data)
        {
            string[] info = Regex.Split(data, "\n")[1].Split('|');

            AnimeEntry anime = new AnimeEntry();
            EpisodeEntry episode = new EpisodeEntry();
            FileEntry file = (item is HashItem) ?
                new FileEntry((HashItem)item) : (FileEntry)item;

            file.fid = int.Parse(info[0]);
            anime.aid = episode.aid = int.Parse(info[1]);
            episode.eid = file.eid = int.Parse(info[2]);
            file.lid = int.Parse(info[4]);

            file.source = info[5].FormatNullable();
            file.acodec = info[6].Contains("'") ? info[6].Split('\'')[0] : info[6].FormatNullable();
            file.acodec = ExtensionMethods.FormatAudioCodec(file.acodec);
            file.vcodec = ExtensionMethods.FormatVideoCodec(info[7].FormatNullable());
            file.vres = info[8].FormatNullable();

            file.length = double.Parse(info[9]);

            if (!string.IsNullOrEmpty(info[10]) && int.Parse(info[10]) != 0) episode.airdate = double.Parse(info[10]);

            file.state = int.Parse(info[11]);
            episode.watched = file.watched = Convert.ToBoolean(int.Parse(info[12]));
            if (!string.IsNullOrEmpty(info[13]) && int.Parse(info[13]) != 0) file.watcheddate = double.Parse(info[13]);

            anime.eps_total = !string.IsNullOrEmpty(info[14]) ?
                (int.Parse(info[14]) > int.Parse(info[15]) ? int.Parse(info[14]) : int.Parse(info[15])) : int.Parse(info[15]);
            anime.year = info[16].Contains('-') ?
                        (info[16].Split('-')[0] != info[16].Split('-')[1] ? info[16] : info[16].Split('-')[0]) : info[16];
            anime.type = info[17];

            anime.romaji = info[18];
            anime.nihongo = info[19].FormatNullable();
            anime.english = info[20].FormatNullable();

            if (Regex.IsMatch(info[21].Substring(0, 1), @"\D"))
                episode.spl_epno = info[21];
            else
                episode.epno = info[21].Contains('-') ? int.Parse(info[21].Split('-')[0]) : int.Parse(info[21]);

            episode.english = info[22];
            episode.romaji = info[23].FormatNullable();
            episode.nihongo = info[24].FormatNullable();

            if (int.Parse(info[3]) != 0)
                file.Group = new GroupEntry
                {
                    gid = int.Parse(info[3]),
                    group_name = info[25],
                    group_abbr = info[26]
                };

            OnFileInfoFetched(new FileInfoFetchedArgs(anime, episode, file));
        }

        /// <summary>
        /// Executes a given command.
        /// And returns a response.
        /// </summary>
        /// <returns>Response from server.</returns>
        private APIResponse Execute(string cmd)
        {
#if !MOCK_REMOTE_API
            string e_cmd = cmd;
            string e_response = String.Empty;

            if (isLoggedIn)
                e_cmd += (e_cmd.Contains("=") ? "&" : " ") + "s=" + sessionKey;

            data = Encoding.UTF8.GetBytes(e_cmd);
            conn.Send(data, data.Length);
            data = conn.Receive(ref apiserver);

            m_lastCommand = DateTime.Now;
            queryLog.Add(m_lastCommand);

            e_response = Encoding.UTF8.GetString(data, 0, data.Length);
            RETURN_CODE e_code = (RETURN_CODE)int.Parse(e_response.Substring(0, 3));

            switch (e_code)
            {
                case RETURN_CODE.LOGIN_FIRST:
                case RETURN_CODE.ACCESS_DENIED:
                case RETURN_CODE.INVALID_SESSION:
                    isLoggedIn = false;
                    if (Login(user, pass))
                        return Execute(cmd);
                    else
                    {
                        var login = new LoginWindow();
                        login.Show();
                        mainWindow.Close();
                        return new APIResponse();
                    }
                default:                    
                    return new APIResponse { Message = e_response, Code = e_code };
            }
#else
            try
            {
                return mocked_api_responses[cmd.Split(' ')[0]];
            }
            catch (KeyNotFoundException)
            {
                return CreateResponse(RETURN_CODE.ILLEGAL_INPUT_OR_ACCESS_DENIED, "Response not mocked");
            }
#endif
        }

#endregion Private Methods

#region Properties & Static Methods

        public static void AppendDebugLine(string line)
        {
            debugLog.Add(new DebugLine(DateTime.Now.ToLongTimeString(), line.ToString()));
            Debug.WriteLine(String.Format("{0} {1}", DateTime.Now.ToLongTimeString(), line.ToString()));
        }

        public MainWindow MainWindow
        {
            set { mainWindow = value; }
        }

        public IPEndPoint APIServer { get { return apiserver; } }
        public TSObservableCollection<DebugLine> DebugLog { get { return debugLog; } }

        public event FileHashingProgressHandler OnFileHashingProgress
        {
            add { hasher.FileHashingProgress += value; }
            remove { hasher.FileHashingProgress -= value; }
        }

#endregion Properties

    }
}
