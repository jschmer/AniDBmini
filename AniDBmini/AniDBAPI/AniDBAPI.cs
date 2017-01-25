
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
            LOGIN_IGNORED_RETRY_LATER                = 1,    // Used to distinguish between failed login and banned connection
            LOGIN_ACCEPTED                           = 200,
            LOGIN_ACCEPTED_NEW_VERSION               = 201,
            LOGGED_OUT                               = 203,
            RESOURCE                                 = 205,
            STATS                                    = 206,
            TOP                                      = 207,
            UPTIME                                   = 208,
            ENCRYPTION_ENABLED                       = 209,
            MYLIST_ENTRY_ADDED                       = 210,
            MYLIST_ENTRY_DELETED                     = 211,
            ADDED_FILE                               = 214,
            ADDED_STREAM                             = 215,
            EXPORT_QUEUED                            = 217,
            EXPORT_CANCELLED                         = 218,
            ENCODING_CHANGED                         = 219,
            FILE                                     = 220,
            MYLIST                                   = 221,
            MYLIST_STATS                             = 222,
            WISHLIST                                 = 223,
            NOTIFICATION                             = 224,
            GROUP_STATUS                             = 225,
            WISHLIST_ENTRY_ADDED                     = 226,
            WISHLIST_ENTRY_DELETED                   = 227,
            WISHLIST_ENTRY_UPDATED                   = 228,
            MULTIPLE_WISHLIST                        = 229,
            ANIME                                    = 230,
            ANIME_BEST_MATCH                         = 231,
            RANDOM_ANIME                             = 232,
            ANIME_DESCRIPTION                        = 233,
            REVIEW                                   = 234,
            CHARACTER                                = 235,
            SONG                                     = 236,
            ANIMETAG                                 = 237,
            CHARACTERTAG                             = 238,
            EPISODE                                  = 240,
            UPDATED                                  = 243,
            TITLE                                    = 244,
            CREATOR                                  = 245,
            NOTIFICATION_ENTRY_ADDED                 = 246,
            NOTIFICATION_ENTRY_DELETED               = 247,
            NOTIFICATION_ENTRY_UPDATE                = 248,
            MULTIPLE_NOTIFICATION                    = 249,
            GROUP                                    = 250,
            CATEGORY                                 = 251,
            BUDDY_LIST                               = 253,
            BUDDY_STATE                              = 254,
            BUDDY_ADDED                              = 255,
            BUDDY_DELETED                            = 256,
            BUDDY_ACCEPTED                           = 257,
            BUDDY_DENIED                             = 258,
            VOTED                                    = 260,
            VOTE_FOUND                               = 261,
            VOTE_UPDATED                             = 262,
            VOTE_REVOKED                             = 263,
            HOT_ANIME                                = 265,
            RANDOM_RECOMMENDATION                    = 266,
            RANDOM_SIMILAR                           = 267,
            NOTIFICATION_ENABLED                     = 270,
            NOTIFYACK_SUCCESSFUL_MESSAGE             = 281,
            NOTIFYACK_SUCCESSFUL_NOTIFIATION         = 282,
            NOTIFICATION_STATE                       = 290,
            NOTIFYLIST                               = 291,
            NOTIFYGET_MESSAGE                        = 292,
            NOTIFYGET_NOTIFY                         = 293,
            SENDMESSAGE_SUCCESSFUL                   = 294,
            USER_ID                                  = 295,
            CALENDAR                                 = 297,

            PONG                                     = 300,
            AUTHPONG                                 = 301,
            NO_SUCH_RESOURCE                         = 305,
            API_PASSWORD_NOT_DEFINED                 = 309,
            FILE_ALREADY_IN_MYLIST                   = 310,
            MYLIST_ENTRY_EDITED                      = 311,
            MULTIPLE_MYLIST_ENTRIES                  = 312,
            WATCHED                                  = 313,
            SIZE_HASH_EXISTS                         = 314,
            INVALID_DATA                             = 315,
            STREAMNOID_USED                          = 316,
            EXPORT_NO_SUCH_TEMPLATE                  = 317,
            EXPORT_ALREADY_IN_QUEUE                  = 318,
            EXPORT_NO_EXPORT_QUEUED_OR_IS_PROCESSING = 319,
            NO_SUCH_FILE                             = 320,
            NO_SUCH_ENTRY                            = 321,
            MULTIPLE_FILES_FOUND                     = 322,
            NO_SUCH_WISHLIST                         = 323,
            NO_SUCH_NOTIFICATION                     = 324,
            NO_GROUPS_FOUND                          = 325,
            NO_SUCH_ANIME                            = 330,
            NO_SUCH_DESCRIPTION                      = 333,
            NO_SUCH_REVIEW                           = 334,
            NO_SUCH_CHARACTER                        = 335,
            NO_SUCH_SONG                             = 336,
            NO_SUCH_ANIMETAG                         = 337,
            NO_SUCH_CHARACTERTAG                     = 338,
            NO_SUCH_EPISODE                          = 340,
            NO_SUCH_UPDATES                          = 343,
            NO_SUCH_TITLES                           = 344,
            NO_SUCH_CREATOR                          = 345,
            NO_SUCH_GROUP                            = 350,
            NO_SUCH_CATEGORY                         = 351,
            BUDDY_ALREADY_ADDED                      = 355,
            NO_SUCH_BUDDY                            = 356,
            BUDDY_ALREADY_ACCEPTED                   = 357,
            BUDDY_ALREADY_DENIED                     = 358,
            NO_SUCH_VOTE                             = 360,
            INVALID_VOTE_TYPE                        = 361,
            INVALID_VOTE_VALUE                       = 362,
            PERMVOTE_NOT_ALLOWED                     = 363,
            ALREADY_PERMVOTED                        = 364,
            HOT_ANIME_EMPTY                          = 365,
            RANDOM_RECOMMENDATION_EMPTY              = 366,
            RANDOM_SIMILAR_EMPTY                     = 367,
            NOTIFICATION_DISABLED                    = 370,
            NO_SUCH_ENTRY_MESSAGE                    = 381,
            NO_SUCH_ENTRY_NOTIFICATION               = 382,
            NO_SUCH_MESSAGE                          = 392,
            NO_SUCH_NOTIFY                           = 393,
            NO_SUCH_USER                             = 394,
            CALENDAR_EMPTY                           = 397,
            NO_CHANGES                               = 399,

            NOT_LOGGED_IN                            = 403,
            NO_SUCH_MYLIST_FILE                      = 410,
            NO_SUCH_MYLIST_ENTRY                     = 411,
            MYLIST_UNAVAILABLE                       = 412,

            LOGIN_FAILED                             = 500,
            LOGIN_FIRST                              = 501,
            ACCESS_DENIED                            = 502,
            CLIENT_VERSION_OUTDATED                  = 503,
            CLIENT_BANNED                            = 504,
            ILLEGAL_INPUT_OR_ACCESS_DENIED           = 505,
            INVALID_SESSION                          = 506,
            NO_SUCH_ENCRYPTION_TYPE                  = 509,
            ENCODING_NOT_SUPPORTED                   = 519,
            BANNED                                   = 555,
            UNKNOWN_COMMAND                          = 598,

            INTERNAL_SERVER_ERROR                    = 600,
            ANIDB_OUT_OF_SERVICE                     = 601,
            SERVER_BUSY                              = 602,
            NO_DATA                                  = 603,
            TIMEOUT_DELAY_AND_RESUBMIT               = 604,
            API_VIOLATION                            = 666,

            PUSHACK_CONFIRMED                        = 701,
            NO_SUCH_PACKET_PENDING                   = 702,

            VERSION                                  = 998,
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
            { "MYLISTADD", CreateResponse(RETURN_CODE.MYLIST_ENTRY_ADDED, "WHATEVER", "1") },
            { "UPTIME", CreateResponse(RETURN_CODE.UPTIME, "UPTIME", "1503955") },
            { "LOGOUT", CreateResponse(RETURN_CODE.LOGGED_OUT, "LOGGED OUT") },
        };
#endif
        #endregion

        #region Fields

        private MainWindow mainWindow;
        private Ed2k hasher = new Ed2k();

#if !MOCK_REMOTE_API
        private UdpClient conn;
#endif
        private IPEndPoint apiserver = new IPEndPoint(IPAddress.None, 0);
        private DateTime m_lastCommand;

        private Object queueLock = new Object();
        private List<DateTime> queryLog = new List<DateTime>();

        private byte[] data = new byte[1400];
        private bool isLoggedIn;
        private string sessionKey, user, pass;

        private static string LoginAttemptDatetimeFormat = "yyyy/MM/dd HH:mm";


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
        public event FileHashingProgressHandler OnFileHashingProgress
        {
            add { hasher.FileHashingProgress += value; }
            remove { hasher.FileHashingProgress -= value; }
        }

        #endregion Fields

        #region Constructor

        public AniDBAPI(string server, int port, int localPort)
        {
            ThreadPool.SetMaxThreads(1, 1);

            apiserver = new IPEndPoint(IPAddress.Any, localPort);

            while (!isConnected)
            {
                try
                {
#if !MOCK_REMOTE_API
                    conn = new UdpClient(localPort);
                    conn.Client.SendTimeout = 5000;
                    conn.Client.ReceiveTimeout = 5000;
                    // api.anidb.net:9000
                    conn.Connect("api.anidb.net", 9000);
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

        #region ANIDB_API
        #region AUTH
        private static bool LoginAllowed(out string nextAllowedAttemptStr)
        {
            nextAllowedAttemptStr = ConfigFile.Read("NextAllowedLoginAttempt", DateTime.Now.ToString(LoginAttemptDatetimeFormat)).ToString();
            DateTime nextAllowedLoginAttempt;
            try
            {
                nextAllowedLoginAttempt = DateTime.Parse(nextAllowedAttemptStr);
            }
            catch (FormatException)
            {
                nextAllowedLoginAttempt = DateTime.Now;
                nextAllowedAttemptStr = nextAllowedLoginAttempt.ToString(LoginAttemptDatetimeFormat);
            }

            return nextAllowedLoginAttempt <= DateTime.Now;
        }

        private void LoginAccepted(string sessionKey)
        {
            isLoggedIn = true;

            DebugData.AppendApiDebugLine(String.Format("Logged in with session key '{0}'", sessionKey));

            ConfigFile.Write("FailedLoginAttempts", "0");
            ConfigFile.Write("sessionKey", sessionKey);
            ConfigFile.Write("NextAllowedLoginAttempt", DateTime.Now.ToString(LoginAttemptDatetimeFormat));
        }

        public bool Login(string user, string pass)
        {
            this.user = user;
            this.pass = pass;

            string nextAllowedAttemptString;
            if (!LoginAllowed(out nextAllowedAttemptString))
            {
                MessageBox.Show("Login not allowed!\nNext allowed login at " + nextAllowedAttemptString, "Login Failed!", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            try
            {
                sessionKey = ConfigFile.Read("sessionKey").ToString();
                isLoggedIn = true;

                DebugData.AppendApiDebugLine(String.Format("Trying to reuse session with key '{0}'", sessionKey));

                APIResponse uptimeResponse = Execute("UPTIME", false);
                if (uptimeResponse.Code == RETURN_CODE.UPTIME)
                {
                    DebugData.AppendApiDebugLine(String.Format("Successfully reused session key '{0}'", sessionKey));
                    LoginAccepted(sessionKey);
                    return true;
                }
                else
                {
                    DebugData.AppendApiDebugLine(String.Format("Session with key '{0}' expired, trying normal login", sessionKey));
                    isLoggedIn = false;
                }
            }
            catch (KeyNotFoundException)
            {
                // ignore
            }

            APIResponse response = Execute("AUTH user=" + user + "&pass=" + pass + "&protover=3&client=anidbmini&clientver=1&enc=UTF8", false);

            if (response.Code == RETURN_CODE.LOGIN_ACCEPTED || response.Code == RETURN_CODE.LOGIN_ACCEPTED_NEW_VERSION)
            {
                sessionKey = response.Message.Split(' ')[1];

                LoginAccepted(sessionKey);
            }
            else if (response.Code == RETURN_CODE.LOGIN_IGNORED_RETRY_LATER)
            {
                int currentFailedLoginAttempts = ConfigFile.Read("FailedLoginAttempts", "0").ToInt32() + 1;
                ConfigFile.Write("FailedLoginAttempts", currentFailedLoginAttempts.ToString());

                // calculate next login attempt
                int baseLoginDelay = Math.Max(1, ConfigFile.Read("BaseLoginDelayMinutes", "5").ToInt32());
                int loginDelayMinutes = Math.Min(4 * 60, (int)Math.Pow(baseLoginDelay, currentFailedLoginAttempts));

                DateTime nextLoginAttempt = DateTime.Now.AddMinutes(loginDelayMinutes);
                string nextLoginAttemptStr = nextLoginAttempt.ToString(LoginAttemptDatetimeFormat);
                ConfigFile.Write("NextAllowedLoginAttempt", nextLoginAttemptStr);

                // tell user
                MessageBox.Show(response.Message + "\nNext allowed login at " + nextLoginAttemptStr, "Login Failed!", MessageBoxButton.OK, MessageBoxImage.Error);
                isLoggedIn = false;
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
            MessageBoxResult result = MessageBox.Show("Will you come back within 30 minutes?", "Long time close?", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                // Only save session key to be reused on next startup
                ConfigFile.Write("sessionKey", sessionKey);
            }
            else
            {
                APIResponse response = Execute("LOGOUT");
                if (response.Code == RETURN_CODE.LOGGED_OUT)
                {
                    MessageBox.Show("Logged out from anidb!\nDon't try to log in too often or you'll be banned for some time.", "Logged out", MessageBoxButton.OK);
                }
                else
                {
                    MessageBox.Show(String.Format("Failed to log out from anidb!\n{0}", response.Message), "Failure", MessageBoxButton.OK);
                }
            }
        }

        #endregion AUTH

        #region DATA

        /// <summary>
        /// Anime command to create a anime tab from an anime ID.
        /// </summary>
        public void Anime(int animeID)
        {
            PrioritizedAPICommand(new Action(delegate
            {
                APIResponse response = Execute(String.Format("ANIME aid={0}&amask=B2E05EFE400080", animeID));

                if (response.Code == RETURN_CODE.ANIME)
                {
                    AnimeTab aTab = (AnimeTab)mainWindow.Dispatcher.Invoke(new Func<AnimeTab>(delegate { return new AnimeTab(Regex.Split(response.Message, "\n")[1]); }));
                    OnAnimeTabFetched(aTab);
                }
            }));
        }

        public void GetFileData(HashItem item)
        {
            PrioritizedAPICommand(new Action(delegate
            {
                APIResponse response = Execute(String.Format("FILE size={0}&ed2k={1}&fmask=78006A28B0&amask=F0E0F0C0", item.Size, item.Hash));

                if (response.Code == RETURN_CODE.FILE)
                    ParseFileData(item, response.Message);
            }));
        }

        public void GetFileData(FileEntry entry)
        {
            PrioritizedAPICommand(new Action(delegate
            {
                APIResponse response = Execute(String.Format("FILE fid={0}&fmask=78006A28B0&amask=F0E0F0C0", entry.fid));

                if (response.Code == RETURN_CODE.FILE)
                    ParseFileData(entry, response.Message);
            }));
        }

        #endregion DATA

        #region MYLIST

        public void MyListAdd(HashItem item)
        {
            PrioritizedAPICommand(new Action(delegate
            {
#if DEBUG
                DebugData.AppendApiDebugLine(String.Format("Add to mylist: {0} Size={1} ED2K={2}", item.Path, item.Size, item.Hash));
#endif

                APIResponse response = Execute(String.Format("MYLISTADD size={0}&ed2k={1}&viewed={2}&state={3}&edit={4}",
                item.Size, item.Hash, Convert.ToInt32(item.Watched), item.State, Convert.ToInt32(item.Edit)));

                switch (response.Code)
                {
                    case RETURN_CODE.MYLIST_ENTRY_ADDED:
                        DebugData.AppendApiDebugLine(String.Format("Added {0} to mylist", item.Name));

                        GetFileData(item);
                        break;
                    case RETURN_CODE.MYLIST_ENTRY_EDITED:
                        DebugData.AppendApiDebugLine(String.Format("Edited mylist entry for {0}", item.Name));

                        GetFileData(item);
                        break;
                    case RETURN_CODE.FILE_ALREADY_IN_MYLIST: // TODO: add auto edit to options.
                        MyListEntry entry = ParseMyListEntry(response);
                        if (entry != null && (entry.watched != item.Watched || entry.state != item.State))
                        {
                            DebugData.AppendApiDebugLine(String.Format("Mylist entry for {0} already exists, adjusting status", item.Name));

                            item.Edit = true;
                            MyListAdd(item);
                        }
                        else
                        {
                            DebugData.AppendApiDebugLine(String.Format("Mylist entry for {0} already exists", item.Name));

                            GetFileData(item);
                        }
                        return;
                    case RETURN_CODE.NO_SUCH_MYLIST_ENTRY:
                        item.Edit = false;
                        MyListAdd(item);
                        return;
                    case RETURN_CODE.NO_SUCH_FILE:
                        DebugData.AppendApiDebugLine("Error! File not in database");
                        break;
                }
            }));
        }

        public bool MyListDel(int lid)
        {
            bool result = false;
            PrioritizedAPICommand(new Action(delegate
            {
                result = Execute(String.Format("MYLISTDEL lid={0}", lid)).Code == RETURN_CODE.MYLIST_ENTRY_DELETED;
            }));
            return result;
        }

        /// <summary>
        /// Retrieves mylist stats.
        /// </summary>
        /// <returns>Array of stat values.</returns>
        public int[] MyListStats()
        {
            int[] result = null;
            PrioritizedAPICommand(new Action(delegate
            {
                string r_msg = Execute("MYLISTSTATS").Message;
                result = Array.ConvertAll<string, int>(Regex.Split(r_msg, "\n")[1].Split('|'), delegate (string s) { return int.Parse(s); });
            }));
            return result;
        }

        /// <summary>
        /// Retrieves a random anime from a certian criteria.
        /// </summary>
        /// <param name="type">type: 0=from db, 1=watched, 2=unwatched, 3=all mylist</param>
        public void RandomAnime(int type)
        {
            PrioritizedAPICommand(new Action(delegate
            {
                APIResponse response = Execute(String.Format("RANDOMANIME type={0}", type));
                if (response.Code == RETURN_CODE.ANIME)
                {
                    Anime(int.Parse(Regex.Split(response.Message, "\n")[1].Split('|')[0]));
                }
            }));
        }

        #endregion MYLIST

        #region File Hashing

        public HashItem ed2kHash(HashItem item)
        {
            hasher.Clear();
            FileInfo file = new FileInfo(item.Path);

            using (FileStream fs = file.OpenRead())
            {
                DebugData.AppendHashDebugLine("Hashing " + item.Name);
                byte[] temp;

                if ((temp = hasher.ComputeHash(fs)) != null)
                {
                    item.Hash = string.Concat(temp.Select(b => b.ToString("x2")).ToArray());
                    DebugData.AppendHashDebugLine("Ed2k hash: " + item.Hash);

                    return item;
                }
                else
                    DebugData.AppendHashDebugLine("Hashing aborted");

                return null;
            }
        }

        public void cancelHashing()
        {
            hasher.Cancel();
            hasher.Clear();
        }

        #endregion File Hashing
        #endregion

        #region Private Methods

        /// <summary>
        /// Executes an action after a certain amount of time has passed
        /// since the previous command was sent to the server.
        /// </summary>
        /// <param name="todo">Action that will be executed after waiting.</param>
        private void PrioritizedAPICommand(Action Command)
        {
            lock (queueLock)
            {
                double secondsSince = DateTime.Now.Subtract(m_lastCommand).TotalSeconds;
                int timeout = CalculateTimeout();

                if (secondsSince < timeout)
                    Thread.Sleep(TimeSpan.FromSeconds(timeout - secondsSince));

                Command();
            }
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

        private MyListEntry ParseMyListEntry(APIResponse response)
        {
            MyListEntry entry = null;

            string[] messageParts = response.Message.Split('\n');
            if (messageParts.Length > 1)
            {
                string dataString = messageParts[1];
                string[] dataParts = dataString.Split('|');
                if (dataParts.Length == 12)
                {
                    entry = new MyListEntry(dataParts);
                }
            }

            return entry;
        }

        /// <summary>
        /// Executes a given command.
        /// And returns a response.
        /// </summary>
        /// <returns>Response from server.</returns>
        private APIResponse Execute(string cmd, bool retryWithLogin = true)
        {
            m_lastCommand = DateTime.Now;

            string e_cmd = cmd;
            string e_response = String.Empty;

            if (isLoggedIn)
                e_cmd += (e_cmd.Contains("=") ? "&" : " ") + "s=" + sessionKey;

#if DEBUG
            DebugData.AppendApiDebugLine(String.Format("Command: {0}", e_cmd));
#endif

#if !MOCK_REMOTE_API
            data = Encoding.UTF8.GetBytes(e_cmd);

            RETURN_CODE e_code = RETURN_CODE.ACCESS_DENIED;
            try
            {
                conn.Send(data, data.Length);
                data = conn.Receive(ref apiserver);
                e_response = Encoding.UTF8.GetString(data, 0, data.Length);
                e_code = (RETURN_CODE)int.Parse(e_response.Substring(0, 3));
            }
            catch (SocketException e)
            {
                DebugData.AppendApiDebugLine(String.Format("Failed to execute API command: {0}, Exception: {1}", e_cmd, e.Message));
            }

            queryLog.Add(m_lastCommand);


#if DEBUG
            DebugData.AppendApiDebugLine(String.Format("Response: {0}", e_response));
#endif

            switch (e_code)
            {
                case RETURN_CODE.LOGIN_FIRST:
                case RETURN_CODE.ACCESS_DENIED:
                case RETURN_CODE.INVALID_SESSION:
                    isLoggedIn = false;
                    if (retryWithLogin)
                    {
                        if (Login(user, pass))
                            return Execute(cmd);
                        else
                        {
                            return new APIResponse();
                        }
                    }
                    else
                    {
                        return new APIResponse { Message = e_response, Code = e_code };
                    }
                default:
                    return new APIResponse { Message = e_response, Code = e_code };
            }
#else
            APIResponse response;
            try
            {
                response = mocked_api_responses[cmd.Split(' ')[0]];
            }
            catch (KeyNotFoundException)
            {
                response = CreateResponse(RETURN_CODE.ILLEGAL_INPUT_OR_ACCESS_DENIED, "Response not mocked");
            }

#if DEBUG
            DebugData.AppendApiDebugLine(String.Format("Response: {0}", response.Message));
#endif

            return response;
#endif
        }

        #endregion Private Methods

        #region Properties & Static Methods

        public MainWindow MainWindow
        {
            get { return mainWindow; }
            set { mainWindow = value; }
        }

        public IPEndPoint APIServer { get { return apiserver; } }

        #endregion Properties

    }
}
