using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using AniDBmini;
using AniDBmini.HashAlgorithms;
using AniDBmini.Collections;

namespace AniDBAPI
{
    class FileHashing
    {
        private Ed2k hasher = new Ed2k();
        public event FileHashingProgressHandler OnFileHashingProgress
        {
            add { hasher.FileHashingProgress += value; }
            remove { hasher.FileHashingProgress -= value; }
        }

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

    }
}
