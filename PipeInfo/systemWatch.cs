using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PipeInfo
{
    public class FileWatcher
    {
         public void initWatcher(string path)
        {
            var watcher = new FileSystemWatcher();  //1. FileSystemWatcher 생성자 호출 

            watcher.Path = path;  //2. 감시할 폴더 설정(디렉토리)

            // 3. 감시할 항목들 설정 (파일 생성, 크기, 이름., 마지막 접근 변경등..)
            watcher.NotifyFilter = NotifyFilters.FileName |
                                    NotifyFilters.DirectoryName |
                                    NotifyFilters.Size |
                                    NotifyFilters.LastAccess |
                                    NotifyFilters.CreationTime |
                                    NotifyFilters.LastWrite;

            //감시할 파일 유형 선택 예) *.* 모든 파일
            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;

            // 4. 감시할 이벤트 설정 (생성, 변경..)
            watcher.Created += new FileSystemEventHandler(Changed);
            watcher.Changed += new FileSystemEventHandler(Changed);
            watcher.Renamed += new RenamedEventHandler(Renamed);

            // 5. FIleSystemWatcher 감시 모니터링 활성화
            watcher.EnableRaisingEvents = true;

        }


        // 6. 감시할 폴더 내부 변경시 event 호출
        private void Changed(object source, FileSystemEventArgs e)
        {
            try
            {
                FileStream fs = new FileStream(e.FullPath, FileMode.Open, FileAccess.ReadWrite);                               
                int len = 0;
                if (File.Exists(e.FullPath))
                {
                    UTF8Encoding temp = new UTF8Encoding(true);

                    StreamReader sr = new StreamReader(fs);
                    StreamWriter sw = new StreamWriter(fs);
                    string str = string.Empty;
                   


                    string[] arrLine = File.ReadAllLines(e.FullPath, Encoding.Default);
                    arrLine[3 - 1] = "안녕";
                    File.WriteAllLines(e.FullPath, arrLine, Encoding.Default);

                    if (str.Contains("MANIFOLD_SOLID_BREP"))
                    {
                        MessageBox.Show(str);
                        sw.WriteLine("바꿧다");
                    }


                    //sw.Close();
                    //fs.Close();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }
        
        private void Renamed(object source, RenamedEventArgs e)
        {
            MessageBox.Show(e.FullPath);
        }

    }
}
