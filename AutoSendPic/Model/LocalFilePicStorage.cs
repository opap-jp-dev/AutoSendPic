using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace AutoSendPic.Model
{
    public class LocalFilePicStorage:PicStorage
    {
        private readonly string LocationLogFileName = "Location.log";

        private static volatile object logSyncObj = new object();

        public string OutputDir
        {
            get;
            set;
        }

        public override async Task<bool> Save(PicData dataToSave)
        {
            //出力ファイル名の決定
            string fileDir = OutputDir;
            if (fileDir[fileDir.Length - 1] != Path.DirectorySeparatorChar)
            {
                fileDir += Path.DirectorySeparatorChar;
            }
            string fileName = MakeFileName(dataToSave.TimeStamp);
            string filePath = fileDir + fileName;

            //保存先がなければ作る
            if (!Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            await Task.Run(() =>
            {
                //保存処理
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(dataToSave.Data, 0, dataToSave.Data.Length);
                }

                //位置情報ログを保存
                lock (logSyncObj) //ログ同時書き込み対策
                {
                    using (FileStream fs = new FileStream(fileDir + LocationLogFileName, FileMode.Append, FileAccess.Write))
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        LocationData loc = dataToSave.Location;
                        sw.WriteLine("{0:yyyy/MM/dd HH:mm:ss},{1},{2},{3},{4},{5},{6},{7},{0:yyyy/MM/dd HH:mm:ss}",
                                    dataToSave.TimeStamp,
                                    fileName,
                                    loc.Provider,
                                    loc.Accuracy,
                                    loc.Altitude,
                                    loc.Latitude,
                                    loc.Longitude,
                                    loc.Speed,
                                    loc.Time);
                    }
                }
            });

            return true;

        }
        


    }
}
