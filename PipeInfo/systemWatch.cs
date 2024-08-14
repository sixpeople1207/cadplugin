using EnvDTE;
using Microsoft.Office.Interop.Excel;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace PipeInfo
{
    public class FileWatcher
    {
        List<string> _spool_Li= new List<string>();
        List<string> _handle_Li = new List<string>();
        List<double> _spoolLenth_Li = new List<double>();
        string _file_path=string.Empty;
        string _file_Name = string.Empty;
        string _full_path = string.Empty;
        public void initWatcher(string path)
        {
            var watcher = new FileSystemWatcher();  //1. FileSystemWatcher 생성자 호출 
            string path_split = Path.GetDirectoryName(path);
            _file_Name = Path.GetFileNameWithoutExtension(path);
            watcher.Path = path_split;  //2. 감시할 폴더 설정(디렉토리)
            _full_path = path;
            //_file_path = path;
            // 3. 감시할 항목들 설정 (파일 생성, 크기, 이름., 마지막 접근 변경등..)
            watcher.NotifyFilter = //NotifyFilters.FileName |
                                   //NotifyFilters.DirectoryName |
                                    NotifyFilters.Size |
                                    //NotifyFilters.LastAccess |
                                    NotifyFilters.CreationTime;// |
                                    //NotifyFilters.LastWrite;

            //감시할 파일 유형 선택 예) *.* 모든 파일
            watcher.Filter = "*.STP*";
            watcher.IncludeSubdirectories = false;

            // 4. 감시할 이벤트 설정 (생성, 변경..)
            watcher.Created += new FileSystemEventHandler(Changed);
            //watcher.Changed += new FileSystemEventHandler(Changed);
            //watcher.Renamed += new RenamedEventHandler(Renamed);

            // 5. FIleSystemWatcher 감시 모니터링 활성화
            watcher.EnableRaisingEvents = true;

        }
        public void ReceiveSpoolList(List<string> spool_Li, List<string> handle_Li, List<double> spoolLength_Li)
        {
            _spool_Li = spool_Li;
            _handle_Li = handle_Li;
            _spoolLenth_Li = spoolLength_Li; //스풀 정보에 길이값 표시를 대비.
        }
        
        public bool stepFileWriteSpoolNumber()
        {
            bool isWrite = false;
            try
            {
                //만들어진 파일경로를 감시해서 가져온다.
                //FileStream ReadData = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                // ReadData.Close();
                //if (File.Exists(filepath))
                //{
                //FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                //string[] arrLine = File.ReadLines(filepath, Encoding.UTF8).ToArray();
                //string[] new_File = new string[arrLine.Length];
                List<string> st = new List<string>();
                // 스풀넘버를 적기 위해 '로 SPLIT으로 나눈다.
                // #29=MANIFOLD_SOLID_BREP( , F8 ,#584); 의 순으로 나누어 지고
                // 나중에 스풀넘버 좌우에 ''를 적어줘야한다. 
                string[] new_LineSp = new string[3];
                string new_Line = string.Empty;
                    using (var reader = new StreamReader(_full_path, Encoding.UTF8))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.Contains("MANIFOLD_SOLID_BREP"))
                            {
                                new_LineSp = line.Split('\'');
                                for (int j = 0; j < _handle_Li.Count; j++)
                                {
                                    if (new_LineSp[1].Contains(_handle_Li[j]))
                                    {
                                    //new_Line = new_LineSp[0] + "\'" + _spool_Li[j] +" "+ _handle_Li[j]+ " " + _spoolLenth_Li[j] +  "\'" + new_LineSp[2];
                                    // 24.7.1양식 결정 
                                    // 24.8 컷팅기 스풀번호 인식리미트 > 도면번호+스풀번호:길이
                                    new_Line = new_LineSp[0] + "\'" + _spool_Li[j] + ":"+ Math.Round(_spoolLenth_Li[j],0) + "\'" + new_LineSp[2];
                                    st.Add(new_Line);
                                }
                                }
                            }
                            else
                            {
                                // 바뀌지 않은 부분은 그대로 적어준다.
                                st.Add(line);
                            }
                        }
                    }

                //string filename = Path.GetDirectoryName(_full_path)+"\\" + Path.GetFileNameWithoutExtension(_full_path) + "_스풀넘버.STP";
                File.WriteAllLines(Path.GetDirectoryName(_full_path) + "\\" + Path.GetFileNameWithoutExtension(_full_path) + "_스풀넘버.STP", st.ToArray());
                isWrite = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("경고:파일이 사용중입니다.");
            }
            return isWrite;
        }
        // 6. 감시할 폴더 내부 변경시 event 호출
        public void Changed(object source, FileSystemEventArgs e)
        {
            try
            {
                //만들어진 파일경로를 감시해서 가져온다.
                string filepath = e.FullPath;

                if (Path.GetFileNameWithoutExtension(filepath).Contains("_")==false)
                {
                    //FileStream ReadData = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    // ReadData.Close();
                    //if (File.Exists(filepath))
                    //{
                    //FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    //string[] arrLine = File.ReadLines(filepath, Encoding.UTF8).ToArray();
                    //string[] new_File = new string[arrLine.Length];
                    List<string> st = new List<string>();
                    // 스풀넘버를 적기 위해 '로 SPLIT으로 나눈다.
                    // #29=MANIFOLD_SOLID_BREP( , F8 ,#584); 의 순으로 나누어 지고
                    // 나중에 스풀넘버 좌우에 ''를 적어줘야한다. 
                    string[] new_LineSp = new string[3];
                    string new_Line = string.Empty;

                    using (var reader = new StreamReader(filepath, Encoding.UTF8))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = reader.ReadLine();
                            if (line.Contains("MANIFOLD_SOLID_BREP"))
                            {
                                new_LineSp = line.Split('\'');
                                for (int j = 0; j < _handle_Li.Count; j++)
                                {
                                    if (new_LineSp[1].Contains(_handle_Li[j]))
                                    {
                                        new_Line = new_LineSp[0] + "\'" + _spool_Li[j] + "\'" + _handle_Li[j] + new_LineSp[2];
                                        st.Add(new_Line);
                                    }
                                }
                            }
                            else
                            {
                                // 바뀌지 않은 부분은 그대로 적어준다.
                                st.Add(line);
                            }
                        }
                    }
                    File.WriteAllLines(Path.GetDirectoryName(filepath) + Path.GetFileNameWithoutExtension(filepath) + "_.STP", st.ToArray());
                }
                //File.WriteAllLines(_file_path, st.ToArray());
                
                }
                    //while (arrLine.Length == 0)
                    //{
                    //    arrLine = File.ReadAllLines(filepath, Encoding.UTF8);
                    //}



                    //List<int> count_Line = new List<int>();
                    //if (arrLine.Length > 0)
                    //{
                    //    for (int i = 0; i < arrLine.Length; i++)
                    //    {
                    //        // 기존 파일에 스풀 정보를 적어준다. "MANIFOLD_SOLID_BREP" 해당열이 CAD에서 객체의 핸들정보를 담고 있는부분.
                    //        // 이 부분과 Spool Information과 연결해준다.
                    //        if (arrLine[i].Contains("MANIFOLD_SOLID_BREP"))
                    //        {
                    //            count_Line.Add(i);
                    //            new_LineSp = arrLine[i].Split('\'');
                    //            for (int j = 0; j < _handle_Li.Count; j++)
                    //            {
                    //                if (new_LineSp[1].Contains(_handle_Li[j]))
                    //                {
                    //                    new_Line = new_LineSp[0] + "\'" + _Spool_Li[j] + "\'" + _handle_Li[j] + new_LineSp[2];
                    //                    new_File[i] = new_Line;
                    //                }
                    //            }
                    //        }
                    //        else
                    //        {
                    //            // 바뀌지 않은 부분은 그대로 적어준다.
                    //            new_File[i] = arrLine[i];
                    //        }
                    //    }
                    //    //using (var sw = new StreamWriter(filepath))
                    //    //{
                    //    //    sw.Write(new_File);
                    //    //}
                    //    //File.WriteAllLines(Path.GetDirectoryName(filepath) + Path.GetFileNameWithoutExtension(filepath)+"_수정.STP", new_File, Encoding.UTF8);
                    //}
                    //else
                    //{
                    //    MessageBox.Show("파일이생성전이다.");
                    //}
                //}

                    // 수정된 STEP파일을 다시 써준다. 
                    // MessageBox.Show(Path.GetDirectoryName(filepath) + Path.GetFileNameWithoutExtension(filepath) + "_SpoolNum");
                    //File.WriteAllLines(Path.GetDirectoryName(path)+Path.GetFileNameWithoutExtension(path)+"_SpoolNum", new_File, Encoding.UTF8);
            
            catch (Exception ex)
            {
                MessageBox.Show("경고:파일이 사용중입니다.");
            }
        }
        private void Renamed(object source, RenamedEventArgs e)
        {
            MessageBox.Show(e.FullPath);
        }
    }
}
