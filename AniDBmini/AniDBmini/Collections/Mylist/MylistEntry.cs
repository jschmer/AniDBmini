
#region Using Statements

using System;
using System.ComponentModel;
using System.Data.SQLite;

#endregion Using Statements

namespace AniDBmini.Collections
{
    public class MyListEntry : INotifyPropertyChanged
    {
        #region Properties

        public int lid { get; set; }
        public int fid { get; set; }
        public int eid { get; set; }

        public int aid { get; set; }
        public int gid { get; set; }

        public UnixTimestamp date { get; set; }

        /// <summary>
        /// <para>0 - unknown - state is unknown or the user doesn't want to provide this information</para>
        /// <para>1 - on hdd - the file is stored on hdd (but is not shared)</para>
        /// <para>2 - on cd - the file is stored on cd</para>
        /// <para>3 - deleted - the file has been deleted or is not available for other reasons (i.e. reencoded)</para>
        /// </summary>
        public int state;

        public bool watched { get { return watcheddate != new UnixTimestamp(0); } }
        public UnixTimestamp watcheddate { get; private set; }

        public string storage { get; set; }
        public string source { get; set; }
        public string other { get; set; }

        public int filestate { get; set; }

        #endregion Properties

        #region Constructors

        public MyListEntry() { }

        /// <summary>
        /// Used for an entry that is to be inserted into the database
        /// from a recently hashed HashItem.
        /// </summary>
        public MyListEntry(string[] dataParts)
        {
            // {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
            if (dataParts.Length != 12)
            {
                throw new Exception("Not supported size for dataParts in MyListEntry!");
            }

            lid = int.Parse(dataParts[0]);
            fid = int.Parse(dataParts[1]);
            eid = int.Parse(dataParts[2]);
            aid = int.Parse(dataParts[3]);
            gid = int.Parse(dataParts[4]);

            date = new UnixTimestamp(int.Parse(dataParts[5]));

            state = int.Parse(dataParts[6]);

            watcheddate = new UnixTimestamp(int.Parse(dataParts[7]));

            storage = dataParts[8];
            source = dataParts[9];
            other = dataParts[10];

            filestate = int.Parse(dataParts[11]);
        }

        #endregion Constructors

        #region INotifyPropertyChanged

        /// <summary>
        /// event for INotifyPropertyChanged.PropertyChanged
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// raise the PropertyChanged event
        /// </summary>
        /// <param name="propName"></param>
        protected void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }

        #endregion

    }
}
