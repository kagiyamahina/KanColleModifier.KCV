﻿using Fiddler;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace Gizeta.KanColleModifier.KCV
{
    public class ModifierViewModel : INotifyPropertyChanged
    {
        private static ModifierViewModel instance = new ModifierViewModel();

        private Dictionary<string, string> data = new Dictionary<string, string>();
        private string ipAddress = "";

        [DllImport(@"wininet.dll", SetLastError = true)]
        public static extern long DeleteUrlCacheEntry(string lpszUrlName);

        private ModifierViewModel()
        {
            readModifierData();
            setModifier();
            if (File.Exists("modifier.enable"))
            {
                ModifierOn = true;
            }
        }

        public static ModifierViewModel Instance
        {
            get { return instance; }
        }

        public void Initialize()
        {
            readModifierData();
        }

        public string ModifierButtonContent
        {
            get { return modifierOn ? "关闭魔改" : "开启魔改"; }
        }

        public string ModifierListContent
        {
            get
            {
                string str = "魔改文件：";
                foreach (var d in data)
                {
                    str += "\r\n" + d.Value;
                }
                return str;
            }
        }

        private bool modifierOn = false;
        public bool ModifierOn
        {
            get { return modifierOn; }
            set
            {
                if (modifierOn != value)
                {
                    modifierOn = value;
                    OnPropertyChanged("ModifierButtonContent");
                    if (value)
                    {
                        if (!File.Exists("modifier.enable"))
                        {
                            File.Create("modifier.enable").Close();
                        }
                    }
                    else
                    {
                        if (File.Exists("modifier.enable"))
                        {
                            File.Delete("modifier.enable");
                        }
                    }
                }
            }
        }

        public ICommand SwitchModifierOn
        {
            get { return new RelayCommand(() => ModifierOn = !ModifierOn); }
        }

        public ICommand UpdateModifierList
        {
            get { return new RelayCommand(() => readModifierData() ); }
        }

        public ICommand CleanCache
        {
            get
            {
                return new RelayCommand(() =>
                {
                    foreach (var item in data)
                    {
                        /* KCV竟没给Graph建Model，先偷个懒 */
                        DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=1");
                        DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=2");
                        DeleteUrlCacheEntry("http://" + ipAddress + "/kcs/resources/swf/ships/" + item.Key + ".swf?VERSION=3");
                    }
                });
            }
        }
        
        #region private method

        private void readModifierData()
        {
            if (File.Exists("魔改.txt"))
            {
                data.Clear();
                var file = File.Open("魔改.txt", FileMode.Open);
                Encoding enc = getEncoding(file);
                file.Close();
                file = File.Open("魔改.txt", FileMode.Open); // 重新打开文件流
                using (var stream = new StreamReader(file, enc))
                {
                    while (!stream.EndOfStream)
                    {
                        var str = stream.ReadLine();
                        var st = str.LastIndexOf('\\') + 1;
                        var ed = str.LastIndexOf(".hack.swf");
                        if (st > 0 && ed > 0)
                        {
                            if (File.Exists(str))
                            {
                                data.Add(str.Substring(st, ed - st), str);
                            }
                        }
                    }
                }
                file.Close();
                OnPropertyChanged("ModifierListContent");
            }
            else
            {
                ModifierOn = false;
            }
        }

        private Encoding getEncoding(FileStream fs)
        {
            BinaryReader r = new BinaryReader(fs, Encoding.Default);
            byte[] ss = r.ReadBytes(4);
            r.Close();
            if (ss[0] <= 0xEF)
            {
                if (ss[0] == 0xEF && ss[1] == 0xBB & ss[2] == 0xBF)
                {
                    return Encoding.UTF8;
                }
                else if (ss[0] == 0xFE && ss[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode;
                }
                else if (ss[0] == 0xFF && ss[1] == 0xFE)
                {
                    return Encoding.Unicode;
                }
                else
                {
                    return Encoding.Default;
                }
            }
            else
                return Encoding.Default;
        }

        #region Fiddler

        private void setModifier()
        {
            FiddlerApplication.BeforeRequest += FiddlerApplication_BeforeRequest;
            FiddlerApplication.BeforeResponse += FiddlerApplication_BeforeResponse;
        }

        private void FiddlerApplication_BeforeResponse(Session oSession)
        {
            if (ModifierOn && oSession.fullUrl.IndexOf("/kcs/resources/swf/ships/") >= 0)
            {
                var tmp1 = oSession.fullUrl.Split('/');
                var tmp2 = tmp1.Last().Split('.');
                if (tmp2.Length >= 1)
                {
                    if (data.ContainsKey(tmp2[0]))
                    {
                        oSession.utilDecodeResponse();
                        oSession.ResponseBody = File.ReadAllBytes(data[tmp2[0]]);
                        oSession.oResponse.headers.HTTPResponseCode = 200;
                        oSession.oResponse.headers.HTTPResponseStatus = "200 OK";
                    }
                }
            }
        }

        private void FiddlerApplication_BeforeRequest(Session oSession)
        {
            if (ipAddress == "" && (oSession.fullUrl.Contains("/kcs/resources") || oSession.fullUrl.Contains("/kcsapi/")))
            {
                var ip = oSession.fullUrl.IndexOf("://") + 3;
                ipAddress = oSession.fullUrl.Substring(ip, oSession.fullUrl.IndexOf("/kcs") - ip);
            }
            if (ModifierOn && oSession.fullUrl.IndexOf("/kcs/resources/swf/ships/") >= 0)
            {
                var tmp1 = oSession.fullUrl.Split('/');
                var tmp2 = tmp1.Last().Split('.');
                if (tmp2.Length >= 1)
                {
                    if (data.ContainsKey(tmp2[0]))
                    {
                        oSession.bBufferResponse = true;
                    }
                }
            }
        }

        #endregion

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}