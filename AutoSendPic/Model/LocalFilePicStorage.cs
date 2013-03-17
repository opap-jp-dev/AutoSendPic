using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace AutoSendPic.Model
{
    public class LocalFilePicStorage:PicStorage
    {

        public string OutputDir
        {
            get;
            set;
        }

        public override bool Save(Stream dataToSave)
        {
            //出力ファイル名の決定
            string fileDir = OutputDir;
            if (fileDir[fileDir.Length - 1] != Path.DirectorySeparatorChar)
            {
                fileDir += Path.DirectorySeparatorChar;
            }
            string filePath = fileDir + MakeFileName();

            //保存先がなければ作る
            if (Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            //保存処理
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                byte[] buf = new byte[4 * 1024];
                int len = 0;
                while (0 < (len = dataToSave.Read(buf, 0, buf.Length)))
                {
                    fs.Write(buf, 0, len);
                }
            }

            return true;
        }
        

        public event EventHandler<Exception> Error;

    }
}
