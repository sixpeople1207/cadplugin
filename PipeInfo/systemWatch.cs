using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PipeInfo
{
    public class FileWatcher
    {
        List<string> _Spool_Li= new List<string>();
        List<string> _handle_Li = new List<string>();

        public void initWatcher(string path)
        {
            var watcher = new FileSystemWatcher();  //1. FileSystemWatcher 생성자 호출 

            watcher.Path = path;  //2. 감시할 폴더 설정(디렉토리)

            // 3. 감시할 항목들 설정 (파일 생성, 크기, 이름., 마지막 접근 변경등..)
            watcher.NotifyFilter = NotifyFilters.FileName; //|
                                    //NotifyFilters.DirectoryName |
                                    //NotifyFilters.Size |
                                    //NotifyFilters.LastAccess |
                                    //NotifyFilters.CreationTime |
                                    //NotifyFilters.LastWrite;

            //감시할 파일 유형 선택 예) *.* 모든 파일
            watcher.Filter = "*.STP*";
            watcher.IncludeSubdirectories = true;

            // 4. 감시할 이벤트 설정 (생성, 변경..)
            watcher.Created += new FileSystemEventHandler(Changed);
            watcher.Changed += new FileSystemEventHandler(Changed);
            watcher.Renamed += new RenamedEventHandler(Renamed);

            // 5. FIleSystemWatcher 감시 모니터링 활성화
            watcher.EnableRaisingEvents = true;

        }
        public void ReceiveSpoolList(List<string> Spool_Li, List<string> handle_Li)
        {
            _Spool_Li = Spool_Li;
            _handle_Li = handle_Li;
            foreach(var sp in Spool_Li)
            {
                MessageBox.Show(sp.ToString());
            }
        }
        // 6. 감시할 폴더 내부 변경시 event 호출
        private void Changed(object source, FileSystemEventArgs e)
        {

            try
            {
                if (File.Exists(e.FullPath))
                {
                    string filepath = e.FullPath;
                    //FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);                               

                    string str = string.Empty;
                    string[] arrLine = File.ReadAllLines(filepath, Encoding.UTF8);
                    string[] new_File = new string[arrLine.Length];

                    // 스풀넘버를 적기 위해 '로 SPLIT으로 나눈다.
                    // #29=MANIFOLD_SOLID_BREP( , F8 ,#584); 의 순으로 나누어 지고
                    // 나중에 스풀넘버 좌우에 ''를 적어줘야한다. 
                    string[] new_LineSp = new string[3];
                    string new_Line = string.Empty;

                    List<int> count_Line = new List<int>();

                    for(int i=0; i < arrLine.Length; i++)
                    {
                        // 기존 파일에 스풀 정보를 적어준다. "MANIFOLD_SOLID_BREP" 해당열이 CAD에서 객체의 핸들정보를 담고 있는부분.
                        // 이 부분과 Spool Information과 연결해준다.
                        if (arrLine[i].Contains("MANIFOLD_SOLID_BREP"))
                        {
                            count_Line.Add(i);
                            new_LineSp = arrLine[i].Split('\'');
                            new_Line = new_LineSp[0]+"'바꿨다_2'"+ new_LineSp[2];
                            new_File[i] = new_Line;
                        }
                        else
                        {
                            // 바뀌지 않은 부분은 그대로 적어준다.
                            new_File[i] = arrLine[i];
                        }
                    }
                    // 수정된 STEP파일을 다시 써준다. 
                    // MessageBox.Show(Path.GetDirectoryName(filepath) + Path.GetFileNameWithoutExtension(filepath) + "_SpoolNum");
                    //File.WriteAllLines(Path.GetDirectoryName(path)+Path.GetFileNameWithoutExtension(path)+"_SpoolNum", new_File, Encoding.UTF8);
                    File.WriteAllLines("D:\\djㅇㅇjd.stp", new_File, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show("파일이 다른 프로그램에서 사용중입니다.");
            }
        }
        private void Renamed(object source, RenamedEventArgs e)
        {
            MessageBox.Show(e.FullPath);
        }
    }
}
