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

        public string OutputDir
        {
            get;
            set;
        }

        public override bool Save(PicData dataToSave)
        {
            //出力ファイル名の決定
            string fileDir = OutputDir;
            if (fileDir[fileDir.Length - 1] != Path.DirectorySeparatorChar)
            {
                fileDir += Path.DirectorySeparatorChar;
            }
            string filePath = fileDir + MakeFileName();

            //保存先がなければ作る
            if (!Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }

            //保存処理
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                fs.Write(dataToSave.Data, 0, dataToSave.Data.Length);
            }

            return true;

        }
        


    }
}
