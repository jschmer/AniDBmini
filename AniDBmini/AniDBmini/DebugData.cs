using System;
using System.Threading;
using System.Diagnostics;

using AniDBmini.Collections;

namespace AniDBmini
{
    static class DebugData
    {
        private static TSObservableCollection<DebugLine> apiDebugLog = new TSObservableCollection<DebugLine>();
        private static TSObservableCollection<DebugLine> hashDebugLog = new TSObservableCollection<DebugLine>();

        public static TSObservableCollection<DebugLine> ApiDebugLog { get { return apiDebugLog; } }
        public static TSObservableCollection<DebugLine> HashDebugLog { get { return hashDebugLog; } }

        public static void AppendApiDebugLine(string line)
        {
            apiDebugLog.Add(new DebugLine(DateTime.Now.ToLongTimeString(), line.ToString()));
            Debug.WriteLine(String.Format("[{0}] {1} {2}", Thread.CurrentThread.ManagedThreadId, DateTime.Now.ToLongTimeString(), line.ToString()));
        }
        public static void AppendHashDebugLine(string line)
        {
            hashDebugLog.Add(new DebugLine(DateTime.Now.ToLongTimeString(), line.ToString()));
            Debug.WriteLine(String.Format("[{0}] {1} {2}", Thread.CurrentThread.ManagedThreadId, DateTime.Now.ToLongTimeString(), line.ToString()));
        }
    }
}
