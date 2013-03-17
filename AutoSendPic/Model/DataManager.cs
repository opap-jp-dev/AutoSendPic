using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace AutoSendPic.Model
{
    public class DataManager
    {

        public List<PicStorage> PicStorages { get; set; }

        public DataManager()
        {
            PicStorages = new List<PicStorage>();
            
        }

        public bool Save(Stream dataToSave)
        {
            bool flgOk = true;

            foreach (var stor in PicStorages)
            {
                flgOk &= stor.Save(dataToSave);
            }

            return flgOk;
        }

    }
}
