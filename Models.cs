﻿/*------------------------------------------------------------------------------------------
 * Model -- класс управления моделями, ведет Журнал Моделей и управляет их сохранением
 * 
 *  5.4.2016 П.Храпкин
 *  
 *--- журнал ---
 * 18.1.2016 заложено П.Храпкин, А.Пасс, А.Бобцов
 * 29.2.2016 bug fix in getGroup
 *  6.3.2016 список Правил в стрке Модели, setModel(name); openModel,readModel
 * 15.3.2016 flag wrToFile in Model class - if true-> we must write down it to file
 * 19.3.2016 use Suppliers and Components classes
 *  5.4.2016 add to Model string field Current Phase; add Mgroup class
 * -----------------------------------------------------------------------------------------
 *      КОНСТРУКТОРЫ: загружают Журнал Моделей из листа Models в TSmatch или из параметров
 * Model(DateTime, string, string, string, string md5, List<Mtch.Rule> r)   - простая инициализация
 * Model( .. )      - указаны все данные модели, кроме даты - записываем в список моделей TSmatch Now
 * Model(doc, n)    - инициализируем экземпляр модели из строки n Документа doc
 * Model(n)         - инициализируем экземпляр модели из строки n TSmatch.xlsx/Models
 *
 *      МЕТОДЫ:
 * Start()         - инициирует начальную загрузку Журнала Моделей; возвращает список имен моделей
 * getModel(name)  - ищет модель по имени name в журнале моделей
 * setModel(name)  - подготавливает обработку модели name; читает все файлы компонентов
 * saveModel(name) - сохраняет модель с именем name
 * UpdateFrTekla   - обновление модели из данных в C# в файловую систему (ЕЩЕ НЕ ОТЛАЖЕНО!!)
 * modelListUpdate(name, dir, Made, MD5) - update list of models in TSmatch.xlsx/Models
 ! openModel()      - open model with OpenFileDialog from File System
 ! readModel(doc)   - read model (TSmatchINFO.xlsx) from dDocument
 * ReсentModel(List<Model> models) - return most recent model in list
 * IsModelCahanged - проверяет, изменилась ли Модель относительно сохраненного MD5
 * lngGroup(atr)   - группирует элементы модели по парам <Материал, Профиль> возвращая массивы длинны 
 */
using System.Collections.Generic;
using System;
using System.Windows.Forms;
using System.Linq;
//!!using System.Text.RegularExpressions;

using Decl = TSmatch.Declaration.Declaration;
using Lib = match.Lib.MatchLib;
using Docs = TSmatch.Document.Document;
using Mtch = TSmatch.Matcher.Matcher;
using TS = TSmatch.Tekla.Tekla;
using Log = match.Lib.Log;
using Msg = TSmatch.Message.Message;
using Supplier = TSmatch.Suppliers.Supplier;
using Component = TSmatch.Components.Component;
using CmpSet = TSmatch.Components.CompSet;

using FileOp = match.FileOp.FileOp;

namespace TSmatch.Model
{
    public class Model : IComparable<Model>
    {
        static List<Model> Models = new List<Model>();  // collection stored in TSmatchINFO.xlsx/Models
        static List<TS.AttSet> Elements = new List<TS.AttSet>();    

        private DateTime date;      // дата и время последнего обновления модели
        public string name;         // название модели
        private string dir;         // каталог в файловой системе, где хранится модель
        private string Made;        // выполненная процедуры TSmatch, после которой получен MD5
        private string Phase;       // текущая фаза проекта. В Tekla это int
        private string MD5;         // контрольная сумма отчета по модели
        public readonly List<Mtch.Rule> Rules;  // список Правил, используемых с данной моделью
        private string strListRules;            // список Правил в виде текста вида "5,6,8"
        public List<Supplier> Suppliers = new List<Supplier>();
        public List<CmpSet> CompSets = new List<CmpSet>();
        private bool wrToFile = true;   // when true - we should write into the file

        public int CompareTo(Model mod) { return mod.date.CompareTo(date); }    //to Sort Models by time

        public Model(DateTime t, string n, string d, string m, string p, string md5, List<Mtch.Rule> r, string s)
        {
            this.date = t;
            this.name = n;
            this.dir = d;
            this.Made = m;
            this.Phase = p;
            this.MD5 = md5;
            this.Rules = r;
            this.strListRules = s;
        }
        public Model(string _name, string _dir, string _made, string _phase, string _md5)
            : this(DateTime.Now, _name, _dir, _made, _phase, _md5, new List<Mtch.Rule>(), "")
        { }
        public Model(string _name, string _dir, string _made, string _phase, string _md5
            , List<Mtch.Rule> _rules, string _strRuleList)
           : this(DateTime.Now, _name, _dir, _made, _phase, _md5, _rules, _strRuleList)
        { }
        public Model(Docs doc, int i)
        {
            this.date  = Lib.getDateTime(doc.Body[i, Decl.MODEL_DATE]);
            this.name  = doc.Body.Strng(i, Decl.MODEL_NAME);
            this.dir   = doc.Body.Strng(i, Decl.MODEL_DIR);
            this.Made  = doc.Body.Strng(i, Decl.MODEL_MADE);
            this.Phase = doc.Body.Strng(i, Decl.MODEL_PHASE);
            this.MD5   =  doc.Body.Strng(i, Decl.MODEL_MD5);
            // преобразуем список Правил из вида "5,6,8" в List<Rule>
            List<Mtch.Rule> _rules = new List<Mtch.Rule>();
            this.strListRules = doc.Body.Strng(i, Decl.MODEL_R_LIST);
            foreach (int n in Mtch.GetPars(this.strListRules))
                _rules.Add(new Mtch.Rule(n));
            this.Rules = _rules; 
        }
        public Model(int i) : this(Docs.getDoc(Decl.MODELS), i) { }
        /// <summary>
        /// Model.Start() - начинает работу со списком моделей, инициализирует структуры данных
        /// </summary>
        /// <returns></returns>
        /// <history>12.2.2016<\history>
        public static List<string> Start()
        {
            Log.set("Model.Start");
            Docs doc = Docs.getDoc(Decl.MODELS);
            for (int i = doc.i0; i <= doc.il; i++)
                if( doc.Body[i, Decl.MODEL_NAME] != null ) Models.Add(new Model(doc, i));
            List<string> strLst = new List<string>();
            foreach (var m in Models) strLst.Add(m.name);
            strLst.Sort();
            Log.exit();
            return strLst;
        }
        /// <summary>
        /// getModel(name) - ищет модель по имени name в журнале моделей 
        /// </summary>
        /// <param name="name">имя искомой модели</param>
        /// <returns>найденную в журнале Модель</returns>
        public static Model getModel(string name)
        {
            Log.set("Model(\"" + name + "\")");
            Model result = null;
            foreach (var md in Models)
                if (md.name == name) { result = md; break; }
            Log.exit();
            return result;
        }
        /// <summary>
        /// saveModel(Model md)  - записываем измененную модель в файловую систему
        /// </summary>
        /// <param name="name">имя модели для записи.</param>
        /// используются вспемогательные перегруженные методы modJrnLine для записи в
        /// существующую строку Models и для добавления новой модели
        public static Model saveModel(string name)
        {
            Log.set("saveModel(\"" + name + "\")");
            Docs doc = Docs.getDoc(Decl.MODELS);
            doc.Reset("Now");
            Models.Sort();
            foreach (var m in Models)
            {
                string t = Lib.timeStr(m.date);
                doc.wrDoc(1, t, m.name, m.dir, m.Made, m.Phase, m.MD5, m.strListRules);
            }
            doc.isChanged = true;
            Docs.saveDoc(doc);
            Log.exit();
            return getModel(name);
        }
        public static Model UpdateFrTekla()
        {
            Log.set(@"UpdateFrTekla()");
            Elements = TS.Read();
            new Log(@"Модель = " + TS.ModInfo.ModelName + "\t" + Elements.Count + " компонентов.");
            string mod_name = TS.ModInfo.ModelName;
            string mod_dir = TS.ModInfo.ModelPath;
            string mod_phase = TS.ModInfo.CurrentPhase.ToString();
            Model mod = modelListUpdate(mod_name, mod_dir, TS.MyName, TS.ModAtrMD5(), mod_phase);
            if (mod.wrToFile) 
            {
                mod.wrModel(Decl.RAW);
                mod.wrModel(Decl.MODELINFO);
                setGroups();            // Group Elements by Materials and Profile
                Mgroup.setMgr();        // Additionally group Groups by Material 
                setModel(mod.name);     // Load price-list for the model
                Mtch.UseRules(mod);     // Search for Model Groups matching Components
                mod.wrModel(Decl.REPORT);
                mod.wrModel(Decl.MODEL_SUPPLIERS);
                saveModel(mod.name);    // а теперь запишем в Журнал Моделей обновленную информацию
            }
            else new Log("------- Эта модель уже есть в TSmatch. Ничего не записываем --------");
            Elements.Clear();
            mod.wrToFile = false;
            Log.exit();
            return mod;
        } // end update
        /// <summary>
        /// modelListUpdate(name, dir, Made, MD5) - update list of models in TSmatch.xlsx/Models
        /// </summary>
        /// <param name="name">Model name</param>
        /// <param name="dir">Model path in File sistem</param>
        /// <param name="Made">version name of TS.Read - important as AttSet field list identifier</param>
        /// <param name="MD5">checksum of all Model parts</param>
        /// <returns>Model, updated in the list of models in TSmatch</returns>
        /// <history> 6.3.2016 PKh
        /// 15.3.16 return Model instead of null in case of completely new model; wrToFile handle
        ///  5.4.16 Current Phase handling
        /// </history>
        static Model modelListUpdate(string name, string dir=null, string Made=null, 
                                     string MD5=null, string Phase = null, string str=null)
        {
            Log.set("modelListUpdate");
            Models.Clear();  Start();        // renowate Models list from TSmatch.xlsx
            Model mod = getModel(name);
            if (mod == null)    // mod==null - means this is completely new model
            {
                Models.Add(new Model(name, dir, Made, Phase, MD5));
                mod = getModel(name);
                mod.wrToFile = true;
            }
            else
            {
                if (dir != null) mod.dir = dir;
                if (Made != null) mod.Made = Made;
                if (Phase != null) mod.Phase = Phase;
                if (MD5 != null) mod.MD5 = MD5;
                if (str != null)
                {
                    mod.strListRules = str;
                    foreach (int n in Mtch.GetPars(str))
                        mod.Rules.Add(new Mtch.Rule(n));
                }
                //!!!!!!!!!!!!!!!!!!!!!!!!!!!! ЗДЕСЬ
                // 1) проверить, доступен ли каталог dir? Если нет -> запустить FileWindowsDialog, потом рекурсивно вызвать modelListUpdate
                // 2) проверить, изменился ли MD5 и список Правил str? Если нет -> список моделей не переписываем, оставляем прежднюю дату
                // 3) читать ModelINFO / MD5 в файле, чтобы понять, нужно ли в него переписать модель (установить флаг wrToFile)

            }
            Log.exit();
            return mod;
        }
        /// <summary>
        /// setModel(name) - подготавливает обработку модели name; читает все файлы компонентов
        /// </summary>
        /// <param name="name">имя модели</param>
        public static void setModel(string name)
        {
            Log.set(@"setModel(" + name + ")");
            Model mod = getModel(name);
            //-- setComp for all Rules of the Model
            foreach (var r in mod.Rules)
            {
                CmpSet cs = r.CompSet.getCompSet();
                mod.CompSets.Add(cs);
                if (!mod.Suppliers.Contains(r.CompSet.Supplier)) mod.Suppliers.Add(r.Supplier);
            }
            foreach (var v in mod.CompSets) v.doc.Close();
            Log.exit();
        }
        public static void openModel()
        {
            Log.set("openModel");
            FolderBrowserDialog ffd = new FolderBrowserDialog();
            string dir = ffd.SelectedPath = ReсentModel(Models).dir;
            DialogResult result = ffd.ShowDialog();
            if (result == DialogResult.OK) dir = ffd.SelectedPath;

            string FileName = "TSmatchINFO.xlsx";
            bool ok = false;
            do
            {
                if (!FileOp.isFileExist(dir, FileName))
                {
                    Msg.W("W20.4_opMod_NO_TSmINFO", dir);
                    OpenFileDialog ofd = new OpenFileDialog();
                    ofd.InitialDirectory = dir;
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        FileName = ofd.FileName;
// !!!!!!                       dir = ofd.Sel
                    }
                }
                ok = readModel(out Elements, dir, FileName);
//!!!                if(!ok) Msg.Ask(Еще раз?) break;
            } while (!ok);
            Log.exit();
        }
        private static bool readModel(out List<TS.AttSet> elm, string dir = null, string FileName = "TSmatchINFO.xlsx")
        {
            Log.set("readModel(" + dir + ", " + FileName + ")");
            bool ok = false;
//!!            Docs.setDocTemplate(Decl.TEMPL_TMP, dir);
            Docs tmp = Docs.getDoc(Decl.TMP_RAW);
            Docs tmpINFO = Docs.getDoc(Decl.TMP_MODELINFO);
            List<TS.AttSet> diff = new List<TS.AttSet>();
            throw new NotImplementedException(); //!!!!!!!!!!!!!!!!!!!!!!!!!!!
            elm = diff;
//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            Log.exit();
            return ok;
        }
        /// <summary>
        /// wrModel(doc_name) - write formatted data from mod to Excel file
        /// </summary>
        /// <param name="doc_name">document to be written name</param>
        /// <history>16.3.2016
        /// 18.3.2016 - write in Excel list of Rules in FORM_RULE
        /// 26.3.2016 - use rule.CompSet.name reference instead of doc.name
        ///  1.4.2016 - re-written
        /// </history>
        public void wrModel(string doc_name)
        {
            Log.set("Model.wrModel(" + doc_name + ")");
            Docs doc = Docs.getDoc(doc_name);
            switch(doc_name)
            {
                case Decl.RAW:
                    int B = 1000, ii = 0, tostr = 1; DateTime t0 = DateTime.Now;
                    foreach (var elm in Elements)
                    {
                        double w = elm.weight;                          // elm.weight - weight [kg];
                        double v = elm.volume / 1000 / 1000 / 1000;     // elm.volume [mm3] -> [m3] 
                        doc.wrDoc(1, elm.guid, elm.mat, elm.mat_type, elm.prf, elm.lng, w, v);
                    }
                    ////////while (ii < Elements.Count)
                    ////////{
                    ////////    var elm = Elements[ii++];
                    ////////    //////doc.Body.AddRow(new object[]
                    ////////    //////    { elm.mat, elm.prf, elm.lng, elm.weight/1000, elm.volume/1000/1000/1000 });
                    ////////    doc.wrDoc(doc.forms[1].name, elm.mat, elm.prf, elm.lng, elm.weight / 1000, elm.volume / 1000 / 1000 / 1000);
                    ////////    if (ii % B == 0)
                    ////////    {
                    ////////        FileOp.saveRngValue(doc.Body, tostr);
                    ////////        tostr += doc.Body.iEOL();
                    ////////        elm = Elements[ii++];
                    ////////        //////doc.Body.Init(new object[]
                    ////////        //////    { elm.mat, elm.prf, elm.lng, elm.weight, elm.volume });
                    ////////        doc.wrDoc(doc.forms[1].name, elm.mat, elm.prf, elm.lng, elm.weight, elm.volume);
                    ////////    }
                    ////////}
                    FileOp.saveRngValue(doc.Body, tostr);
                    new Log("Время записи в файл (помимо чтения из Tekla) t=" + (DateTime.Now - t0).ToString() + " сек");
                    doc.isChanged = true;
                    Model mod = getModel(TS.ModInfo.ModelName);
                    doc.saveDoc(BodySave: false, MD5: mod.MD5, EOL: Elements.Count + 1);
                    break;
                case Decl.MODELINFO:
                    doc.wrDoc(1, name, dir, "фаза?", date, MD5, Elements.Count, strListRules);
                    foreach (var rule in this.Rules)
                    {
                        string supplier = rule.Supplier.name;
                        doc.wrDoc(doc.forms[2].name, supplier, rule.CompSet.name, rule.text);
                    }
                    doc.isChanged = true;
                    doc.saveDoc();
                    break;
                case Decl.REPORT:
                    double sumWgh = 0, sumPrice = 0;
                    int iGr = doc.i0;
                    foreach (var gr in Groups)
                    {
                        double? w = doc.Body.Double(iGr, Decl.REPORT_SUPL_WGT);
                        double? p = doc.Body.Double(iGr++, Decl.REPORT_SUPL_PRICE);
                        sumWgh += (w == null) ? 0 : (double)w;
                        sumPrice += (p == null) ? 0 : (double)p;
                    }
                    doc.wrDoc(2, sumWgh, sumPrice);
                    break;
                case Decl.MODEL_SUPPLIERS:
                    foreach(var s in Suppliers)
                    {
                        doc.wrDoc(1, s.name, s.Url, s.City, s.index, s.street, s.telephone);
                    }
                    doc.isChanged = true;
                    doc.saveDoc();
                    break;
            }
            doc.saveDoc();
            Log.exit();
        }
        /// <summary>
        /// ReсentModel(List<Model> models) -- return most recent model in list
        /// </summary>
        /// <param name="models">model list</param>
        /// <returns>most recently saved Model in the list</returns>
        public static Model ReсentModel(List<Model> models)
        {
            Log.set("ReсentModel");
            Model mod = null;
            if (models.Count > 0)
            {
                models.Sort();
                mod = models[0];
            }
            Log.exit();
            return mod;
        }
        public static string RecentModelDir()
        {
            Models.Sort();
            return Models[0].dir;
        }
        public struct Group : IComparable<Group>
        {
            public string mat, mat_type, prf;
            public double lng, wgt, vol;
            public readonly List<string> GUIDs; // List of ID Parts in the Group

            public Group(string _mat, string _mat_type, string _prf,
                         double _lng, double _wgt, double _vol,
                         List<string> _guids) : this()
            {
                mat = Lib.ToLat(_mat); mat_type = _mat_type; prf = Lib.ToLat(_prf);
                lng = _lng; wgt = _wgt; vol = _vol;
                GUIDs = _guids;
            }
            public int CompareTo(Group grp)     //to Sort Groups by Material and Profile
            {
                int x = this.mat.CompareTo(grp.mat);
                if (x == 0) x = this.prf.CompareTo(grp.prf);
                return x;
            }
        }
        public static List<Group> Groups = new List<Group>();
        public static List<Group> setGroups()
        {
            string mat = "", mat_type = "", prf = "";
            double lng = 0.0, wgt = 0.0, vol = 0.0;
            List<string> guids = new List<string>();
            foreach (var v in Elements)
            {
                if (v.lng == 0.0) continue;
                if (mat == v.mat && prf == v.prf)
                {
                    lng += v.lng;
                    wgt += v.weight/1000;
                    vol += v.volume/1000/1000/1000;
                    guids.Add(v.guid);
                }
                else
                {
                    if (lng > 0.0) Groups.Add(new Group(mat, mat_type, prf, lng, wgt, vol, guids));
                    mat = v.mat; mat_type = v.mat_type; prf = v.prf; lng = v.lng; wgt = v.weight/1000;
                    vol = v.volume/1000/1000/1000;
                    guids.Clear(); guids.Add(v.guid);
                }
            }
            if (lng > 0.0) Groups.Add(new Group(mat, mat_type, prf, lng, wgt, vol, guids));
            return Groups;
        }
        //public static void lngGroup(dynamic atr)
        //{
        //    Log.set("lngGroup");
        //    if (atr.GetType() != typeof(List<TS.AttSet>)) Log.FATAL("ПОКА Я УМЕЮ РАБОТАТЬ ТОЛЬКО С TSread, но вскоре...");
        //    List<TS.AttSet> Elements = atr;
        //    Elements.Sort();
        //    foreach (var elm in Elements)
        //    {
        //        Group grp = new Group(elm.mat, elm.prf);
        //    }
        //    Log.exit();
        //}
        public class Mgroup : IComparable<Mgroup>
        {
            static List<Mgroup> Mgroups = new List<Mgroup>();
            
            String mat;
            double volume, weight;
            List<Group> groups = new List<Group>();

            public Mgroup(string mat, double vol, double wgt, List<Group> grps)
            {
                this.mat = mat;
                this.volume = vol;
                this.weight = wgt;
            }

            public int CompareTo(Mgroup mgr) { return mgr.mat.CompareTo(mgr); }    //to Sort Mgroups by Material

            internal static void setMgr()
            {
                Log.set("setMgr");
                Mgroups.Clear();
                Groups.Sort();
                string mat = "";
                double vol = 0, wgt = 0;
                List<Group> grps = new List<Group>();
                foreach (var g in Groups)
                {
                    if (mat == g.mat)
                    {
                        grps.Add(g);
                        vol += g.vol;
                        wgt += g.wgt;
                    }
                    else
                    {
                        if (mat != "") Mgroups.Add(new Mgroup(mat, vol, wgt, grps));
                        mat = g.mat; vol = 0; wgt = 0;
                        grps = new List<Group>();
                    }
                }
                if (vol > 0) Mgroups.Add(new Mgroup(mat, vol, wgt, grps));
                Log.exit();
            }
        } // end class Mgroup
    } // end class Model
} // end namespace Model
