﻿/*------------------------------------------------------------------------------------------
 * Model -- класс управления моделями, ведет Журнал Моделей и управляет их сохранением
 * 
 *  25.1.2017 П.Храпкин
 *  
 *--- журнал ---
 * 18.1.2016 заложено П.Храпкин, А.Пасс, А.Бобцов
 * 29.2.2016 bug fix in getGroup
 *  6.3.2016 список Правил в стрке Модели, setModel(name); openModel,readModel
 * 15.3.2016 flag wrToFile in Model class - if true-> we must write down it to file
 * 19.3.2016 use Suppliers and Components classes
 *  5.4.2016 add to Model string field Current Phase; add Mgroup class
 *  5.6.2016 OpenModel modified; UpdateFrIFC created
 * 21.6.2016 Group and Mgroup classes moved to ElmAttSet module
 *  6.8.2016 - Add field elements in Model class
 *           - non static methos getModel, saveModel, setModel
 *           - Read(modelName) -- by defaul if(modelName == "") read most recent model
 * 22.8.2016 - Field ifcPath add to Model Journal
 * 29.9.2016 - re-written getGroups()
 * 22.11.2016 - get Recent model from TXmatch.xlsx, not from Models collection
 * 29.11.2016 - HashSet instead of List for Rules and Supplier collections
 * 26.12.2016 - get model from Tekla -- TEMPORAPY PATCH
 * 25.01.2017 - add bool doInit argument to Model constructor to avoid Rule int at doInit=false
 * !!!!!!!!!!!!! -------------- TODO --------------
 * ! избавиться от static в RecentModel, RecentModelDir 
 * -----------------------------------------------------------------------------------------
 *      КОНСТРУКТОРЫ: загружают Журнал Моделей из листа Models в TSmatch или из параметров
 * Model(DateTime, string, string, string, string md5, List<Mtch.Rule> r)   - простая инициализация
 * Model( .. )      - указаны все данные модели, кроме даты - записываем в список моделей TSmatch Now
 * Model(doc, n)    - инициализируем экземпляр модели из строки n Документа doc
 * Model(n)         - инициализируем экземпляр модели из строки n TSmatch.xlsx/Models
 *
 *      МЕТОДЫ:
 * Start()         - инициирует начальную загрузку Журнала Моделей; возвращает список имен моделей
 * Read(modelName) - получение модели (списка элементов с атрибутами) из Tekla или IFC
 * getModel(name)  - ищет модель по имени name в журнале моделей
 * setModel(name)  - подготавливает обработку модели name; читает все файлы компонентов
 * saveModel(name) - сохраняет модель с именем name
 * UpdateFrTekla() - обновление модели из данных в C# в файловую систему (ЕЩЕ НЕ ОТЛАЖЕНО!!)
 * modelListUpdate(name, dir, Made, MD5) - update list of models in TSmatch.xlsx/Models
 ! openModel()      - open model with OpenFileDialog from File System
 ! readModel(doc)   - read model (TSmatchINFO.xlsx) from dDocument
 * ReсentModel(List<Model> models) - return most recent model in list
 * getGroups()      - groupping of elements of Model by Material and Profile
 * IsModelCahanged - проверяет, изменилась ли Модель относительно сохраненного MD5
 ! lngGroup(atr)   - группирует элементы модели по парам <Материал, Профиль> возвращая массивы длинны 
 */
using System.Collections.Generic;
using System;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using log4net;

using Decl = TSmatch.Declaration.Declaration;
using Lib = match.Lib.MatchLib;
using Docs = TSmatch.Document.Document;
using Mtch = TSmatch.Matcher.Mtch;
using TS = TSmatch.Tekla.Tekla;
using Ifc = TSmatch.IFC.IFC;
using Log = match.Lib.Log;
using Msg = TSmatch.Message.Message;
using Elm = TSmatch.ElmAttSet.ElmAttSet;
using ElmMGr  = TSmatch.ElmAttSet.Mgroup;
using ElmGr   = TSmatch.ElmAttSet.Group;
using Supplier = TSmatch.Suppliers.Supplier;
using CmpSet = TSmatch.CompSet.CompSet;
using SType = TSmatch.Section.Section.SType;

using FileOp = match.FileOp.FileOp;

namespace TSmatch.Model
{
    public class Model : IComparable<Model>
    {
        #region --- Definitions and Constructors
        public static readonly ILog log = LogManager.GetLogger("Module");

        static List<Model> Models = new List<Model>();  // collection stored in TSmatchINFO.xlsx/Models

        private DateTime date;      // дата и время последнего обновления модели
        public string name;         // название модели
        private string dir;         // каталог в файловой системе, где хранится модель
        private string ifcPath;     // полный путь к ifc-файлу, соответствующему модели
        private string Made;        // выполненная процедуры TSmatch, после которой получен MD5
        private string Phase;       // текущая фаза проекта. В Tekla это int
        private string MD5;         // контрольная сумма отчета по модели
        private List<Elm> elements = new List<Elm>();
        ////        private Dictionary<string, List<Elm>> elmMatTypeGroups = new Dictionary<string, List<Elm>>();
        private List<ElmMGr> elmMgroups = new List<ElmMGr>();
        public List<ElmGr> elmGroups = new List<ElmGr>();   // will be used in Matcher
        public readonly HashSet<Rule.Rule> Rules = new HashSet<Rule.Rule>();
        private string strListRules;                        // список Правил в виде текста вида "5,6,8"   
        public readonly HashSet<Supplier> Suppliers = new HashSet<Supplier>();
        public List<CmpSet> CompSets = new List<CmpSet>();
        List<Matcher.Mtch> matches = new List<Matcher.Mtch>();
        private bool wrToFile = true;   // when true- we should write into the file TSmatchINFO.xlsx, else- no changes

        public int CompareTo(Model mod) { return mod.date.CompareTo(date); }    //to Sort Models by time

#if DEBUG
        public Model() { }
#endif

        public Model(DateTime t, string n, string d, string ifc, string m, string p, string md5, HashSet<Rule.Rule> r, string s)
        {
            this.date = t;
            this.name = n;
            this.dir = d;
            this.ifcPath = ifc;
            this.Made = m;
            this.Phase = p;
            this.MD5 = md5;
            this.Rules = r;
            this.strListRules = s;
        }
        public Model(string _name, string _dir, string _ifc, string _made, string _phase, string _md5)
            : this(DateTime.Now, _name, _dir, _ifc, _made, _phase, _md5, new HashSet<Rule.Rule>(), "")
        { }

        /// <summary>
        /// newModelOpenDialog(models) -- run when new Model must be open not exists in Model Journal models
        /// </summary>
        /// <param name="models">List<Models> to be updated after dialog</param>
        /// <returns>List<Model></Model>sorted models list -- models[0] - just opened Model</returns>
        /// <history>6.08.2016</history>
        internal static Model newModelOpenDialog(out List<Model> models)
        {
            Model result = null;
            string newFileDir = "", newFileName = "";

            throw new NotImplementedException();

            while (result != null)
            {
                if (FileOp.isFileExist(newFileDir, newFileName))
                {
                }
                models = Start();
//TODO                models.savModelJournal();
            }
            return result;
        }

        public Model(string _name, string _dir, string _ifc, string _made, string _phase, string _md5
            , HashSet<Rule.Rule> _rules, string _strRuleList)
           : this(DateTime.Now, _name, _dir, _ifc, _made, _phase, _md5, _rules, _strRuleList)
        { }
        public Model(Docs doc, int i, bool doInit = true)
        {
            this.date  = Lib.getDateTime(doc.Body[i, Decl.MODEL_DATE]);
            this.name  = doc.Body.Strng(i, Decl.MODEL_NAME);
            this.dir   = doc.Body.Strng(i, Decl.MODEL_DIR);
            this.ifcPath = doc.Body.Strng(i, Decl.MODEL_IFCPATH);
            this.Made  = doc.Body.Strng(i, Decl.MODEL_MADE);
            this.Phase = doc.Body.Strng(i, Decl.MODEL_PHASE);
            this.MD5   =  doc.Body.Strng(i, Decl.MODEL_MD5);
            // преобразуем список Правил из вида "5,6,8" в List<Rule>
            strListRules = doc.Body.Strng(i, Decl.MODEL_R_LIST);
            if (doInit)
            {
                foreach (int n in Lib.GetPars(strListRules))
                    Rules.Add(new Rule.Rule(n));
            }
        }
        public Model(int i, bool doInit = true) : this(Docs.getDoc(Decl.MODELS), i, doInit) { }
        #endregion

        /// <summary>
        /// Model.Start() - начинает работу со списком моделей, инициализирует структуры данных
        /// </summary>
        /// <returns></returns>
        /// <history>12.2.2016<\history>
        public static List<Model> Start()
        {
            Log.set("Model.Start");
            Models.Clear();
            Docs doc = Docs.getDoc(Decl.MODELS);
            for (int i = doc.i0; i <= doc.il; i++)
                if( doc.Body[i, Decl.MODEL_NAME] != null )Models.Add(new Model(doc, i));
            List<string> strLst = new List<string>();
            foreach (var m in Models) strLst.Add(m.name);
            strLst.Sort();
            Log.exit();
            return Models;
        }
        /// <summary>
        /// Read(nameModel) - получение модели (списка элементов с атрибутами) из Tekla или IFC
        /// </summary>
        /// <param name="modelName">имя читаемой модели, по умолчанию - чтение того, что есть</param>
        /// <returns>Model со списком прочитанных элементов в model.elements</returns>
        public Model Read(string modelName = "")
        {
            log.Info("TRACE: Read(\"" + modelName + "\")");
            Model mod = (modelName == "" || name == modelName)? this : getModel(modelName);
            elements = TS.isTeklaActive() ? TS.Read(mod.dir, mod.name) : Ifc.Read(mod.ifcPath);
            log.Info(@"TRACE: Модель = " + mod.name + "\t" + Elm.Elements.Count + " компонентов.");
            return mod;
        }

        internal static Model getModelFrTekla()
        {
            string modName = TS.getModInfo();
            Docs doc = Docs.getDoc(Decl.MODELS);
            for (int i = doc.i0; i <= doc.il; i++)
            {
                if (doc.Body.Strng(i, Decl.MODEL_NAME) != modName) continue;
                return new TSmatch.Model.Model(i);
            }
            Msg.F("TEMPORARY: New model in Tekla! Put it in Model Jornal manually");
            return null;
        }

        /// <summary>
        /// getModel(name) - get Model by name in TSmatch.xlsx/Model Journal  
        /// </summary>
        /// <param name="name">Model name; by default get most recent model of model from Tekla</param>
        /// <returns>found Model</returns>
        /// <history>5.6.2016
        /// 6.8.2016 - non static method</history>
        public Model getModel(string name)
        {
            Log.set("Model(\"" + name + "\")");
            Model result = null;
            if (string.IsNullOrEmpty(name)) result = RecentModel();
            else
            {
                List<Model> models = Start();
                result = models.Find(x => x.name == name);
                while(result==null)
                {
                    result = newModelOpenDialog(out models);
                }
            }
            Log.exit();
            return result;
        }
        /// <summary>
        /// saveModel(Model md)  - записываем измененную модель в файловую систему
        /// </summary>
        /// <param name="name">имя модели для записи.</param>
        /// <history>6.8.16 -- nonstatic method</history>
        public Model saveModel(string name)
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
        public Model UpdateFrTekla()
        {
            Log.set(@"UpdateFrTekla()");
            //            Elm.Elements = TS.Read();
            List<Elm> elements = TS.Read();
            new Log(@"Модель = " + TS.ModInfo.ModelName + "\t" + Elm.Elements.Count + " компонентов.");
            string mod_name = TS.ModInfo.ModelName;
            string mod_dir = TS.ModInfo.ModelPath;
            string mod_phase = TS.ModInfo.CurrentPhase.ToString();
            Model mod = modelListUpdate(mod_name, mod_dir, TS.MyName, Elm.ElementsMD5(), mod_phase);
            mod.elements = elements;
            if (mod.wrToFile) 
            {
                mod.wrModel(Decl.TSMATCHINFO_RAW);
                mod.wrModel(Decl.TSMATCHINFO_MODELINFO);
                ////ElmGr.setGroups();            // Group Elements by Materials and Profile
                ////ElmMGr.setMgr();        // Additionally group Groups by Material 
                setModel(mod.name);     // Load price-list for the model
//revision 2016.12.05                Mtch.UseRules(mod);     // Search for Model Groups matching Components
                mod.wrModel(Decl.TSMATCHINFO_REPORT);
                mod.wrModel(Decl.TSMATCHINFO_SUPPLIERS);
                saveModel(mod.name);    // а теперь запишем в Журнал Моделей обновленную информацию
            }
            else new Log("------- Эта модель уже есть в TSmatch. Ничего не записываем --------");
            Elm.Elements.Clear();
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
        ///  6.8.16 non static method
        /// </history>
        Model modelListUpdate(string name, string dir=null, string Made=null, 
                                     string MD5=null, string Phase = null, string str=null)
        {
            Log.set("modelListUpdate");
            Models.Clear();  Start();        // renowate Models list from TSmatch.xlsx
            Model mod = getModel(name);
            if (mod == null)    // mod==null - means this is completely new model
            {
                Models.Add(new Model(name, dir, ifcPath, Made, Phase, MD5));
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
                    foreach (int n in Lib.GetPars(str))
                        mod.Rules.Add(new TSmatch.Rule.Rule(n));
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
        public void setModel(string name)
        {
            Log.set(@"setModel(" + name + ")");
            Model mod = getModel(name);
            //-- setComp for all Rules of the Model
            foreach (var r in mod.Rules)
            {
                //11.1.17                CmpSet cs = r.CompSet.getCompSet();
                //11.1.17                mod.CompSets.Add(cs);
                if (!mod.Suppliers.Contains(r.CompSet.Supplier)) mod.Suppliers.Add(r.Supplier);
            }
            foreach (var v in mod.CompSets) v.doc.Close();
            Log.exit();
        }
        public void setModel()
        {
            log.Info("TRACE: setModel(\"" + name + "\")");
            //!! временно для отладки запишем в mod.Sopplers ВСЕХ поставщиков. Потом - только тех, кто в RuleList
 //!!           this.Suppliers = TSmatch.Suppliers.Supplier.    //TSmatch.Startup.Bootstrap.init(Decl.SUPPLIERS);
        }
        /// <summary>
        /// OpenModel(name) - open model name from Excel file, Tekla, or ifc file. Selection, when necessary
        /// </summary>
        /// <param name="name">Model name. If empty - open most recent model; when not found -- dialog browth</param>
        /// <history> 5.6.2016 </history>
        const string RECENT_MODEL = "My most recent model";
        public void openModel(string name = RECENT_MODEL)
        {
            Log.set("openModel(\"" + name + "\")");
            Model mod = null;
            bool ok = false;
            if (TS.isTeklaActive())
            {
                //!! here we could upload over API in Tekla another model it differ from requested name
                //!! if (TS.isTeklaModel(mod.name))
                //!! implement it later on 23/6/2016
                UpdateFrTekla();
            }
            if (name == RECENT_MODEL) mod = RecentModel();
                                 else mod = getModel(name);

            string dir = mod.dir;
            string FileName = "TSmatchINFO.xlsx";
            if (mod.Made == "IFC") FileName = "out.ifc";
            if (mod != null) ok = FileOp.isFileExist(dir, FileName);

            if (!ok)
            {           //-- Folder or File Browth dialog
                FolderBrowserDialog ffd = new FolderBrowserDialog();
                dir = ffd.SelectedPath = mod.dir;
                DialogResult result = ffd.ShowDialog();
                if (result == DialogResult.OK) dir = ffd.SelectedPath;
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
                    ok = readModel(dir, FileName);
                    //!!!                if(!ok) Msg.Ask(Еще раз?) break;
                } while (!ok);
            }
            else
            {
                string ext = Path.GetExtension(FileName);
                if ( ext == ".ifc") Ifc.Read(dir, FileName); //!!
                if (ext == ".xlsx") dir= "";//!! readModel();
            }
            Log.exit();
        }
        private static bool readModel(string dir = null, string FileName = "TSmatchINFO.xlsx")
        {
            Log.set("readModel(" + dir + ", " + FileName + ")");
            bool ok = false;
//!!            Docs.setDocTemplate(Decl.TEMPL_TMP, dir);
            Docs tmp = Docs.getDoc(Decl.TMP_RAW);
            Docs tmpINFO = Docs.getDoc(Decl.TMP_MODELINFO);
//!!            List<TS.AttSet> diff = new List<TS.AttSet>();
            throw new NotImplementedException(); //!!!!!!!!!!!!!!!!!!!!!!!!!!!
//!!            elm = diff;
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
        /// 21.8.2016 - case constants defined here from Decl, changed TSmatchINFO Document list, restructured
        /// </history>
        const string WR_MODEL_INFO = Decl.TSMATCHINFO_MODELINFO;
        const string WR_MODEL_RAW = Decl.TSMATCHINFO_RAW;           
        const string WR_MODEL_MATERIALS = Decl.TSMATCHINFO_MATERIALS;
        const string WR_MODEL_SUPPLIERS = Decl.TSMATCHINFO_SUPPLIERS;
        const string WR_MODEL_RULES = Decl.TSMATCHINFO_RULES;       
        const string WR_MODEL_REPORT = Decl.TSMATCHINFO_REPORT; 
        public void wrModel(string doc_name)
        {
            Log.set("Model.wrModel(" + doc_name + ")");
            DateTime t0 = DateTime.Now;
            Docs doc = Docs.getDoc(doc_name);
            switch(doc_name)
            {
                case WR_MODEL_INFO:     // общая информация о модели: имя, директория, MD5 и др
                    doc.wrDocSetForm("HDR_ModelINFO", 1, AutoFit: true);
                    doc.wrDocForm(name, dir, "фаза?", date, MD5, Elm.Elements.Count);
                    break;
                case WR_MODEL_RAW:      // элементы с атрибутами, как они прочитаны из модели
                    doc.wrDocSetForm("FORM_RawLine", 2, AutoFit: true);
                    foreach (var elm in elements)
                    {
                        double w = elm.weight;                          // elm.weight - weight [kg];
                        double v = elm.volume; // / 1000 / 1000 / 1000;     // elm.volume [mm3] -> [m3] 
                        doc.wrDocForm(elm.guid, elm.mat, elm.mat_type, elm.prf, elm.length, w, v);
                    }
                    break;
                case WR_MODEL_MATERIALS:    // сводка по материалам, их типам (бетон, сталь и др)
                    doc.wrDocSetForm("FORM_Materials", 3, AutoFit: true);
                    foreach (var mGr in elmMgroups)
                    {
                        doc.wrDocForm(mGr.mat, mGr.totalVolume, mGr.totalWeight, mGr.totalPrice);
                    }
                    break;
                case WR_MODEL_SUPPLIERS:    // сводка по поставщикам проекта (контакты, URL прайс-листа, закупки)
                    doc.wrDocSetForm("FORM_ModSupplierLine", 4, AutoFit: true);
                    foreach (var s in Suppliers)
                    {
                        doc.wrDocForm(s.name, s.Url, s.City, s.index, s.street, s.telephone);
                    }
                    break;
                case WR_MODEL_RULES:        // перечень Правил, используемых для обработки модели
                    doc.wrDocSetForm("HDR_ModRules", 1, AutoFit: true);
                    doc.wrDocForm(strListRules);
                    doc.wrDocSetForm("FORM_ModRuleLine");
                    foreach (var rule in Rules)
                    {
                        doc.wrDocForm(rule.Supplier.name, rule.CompSet.name, rule.text);
                    }
                    break;
                case WR_MODEL_REPORT:       // отчет по сопоставлению групп <материал, профиль> c прайс-листами поставщиков
                    doc.wrDocSetForm("FORM_Report", AutoFit: true);
                    int n = 1;
                    foreach (var gr in elmGroups)
                    {
                        var comp = gr.match.component;
                        string compDescr = comp.fps[SType.Description].pars[0].par.ToString();
                        doc.wrDocForm(n++, gr.mat, gr.prf
                            , gr.totalLength, gr.totalWeight, gr.totalVolume
                            , compDescr, 0, 0, 0, gr.totalPrice);
                    }
                    //--- string - Summary
                    double sumWgh = 0, sumPrice = 0;
                    int iGr = doc.i0;
                    foreach (var gr in elmGroups)
                    {
                        double? w = doc.Body.Double(iGr, Decl.REPORT_SUPL_WGT);
                        double? p = doc.Body.Double(iGr++, Decl.REPORT_SUPL_PRICE);
                        sumWgh += (w == null) ? 0 : (double)w;
                        sumPrice += (p == null) ? 0 : (double)p;
                    }
                    doc.wrDocSetForm("FORM_Report_Sum", AutoFit: true);
                    doc.wrDocForm( sumWgh, sumPrice);
                    break;
            }
            doc.isChanged = true;
            doc.saveDoc();
            log.Info("Время записи в файл \"" + doc_name + "\"\t t= " + (DateTime.Now - t0).ToString() + " сек");
            Log.exit();
        }
        /// <summary>
        /// ReсentModel(List<Model> models) -- return most recent model in list
        /// </summary>
        /// <param name="models">model list</param>
        /// <returns>most recently saved Model in the list</returns>
        /// <history>
        /// 2016.11.21 - get RecentModel from TOC, not from memory
        /// </history>
        public Model ReсentModel(List<Model> models)
        {
            Log.set("ReсentModel");
            Model mod = null;
            if (models.Count > 0)
            {
                models.Sort();
                mod = models[0];
            }
            else mod = newModelOpenDialog(out models);
            Log.exit();
            return mod;
        }
        public static Model RecentModel()
        {
            string date = "1.1.52";
            int iMod = 0;
            Docs doc = Docs.getDoc(Decl.MODELS);
            for (int i = doc.i0; i <= doc.il; i++)
            {
                if (doc.Body[i, Decl.MODEL_NAME] != null)
                {
                    string modDate = doc.Body.Strng(i, Decl.MODEL_DATE);
                    if (Lib.getDateTime(modDate) > Lib.getDateTime(date))
                    {
                        date = modDate;
                        iMod = i;
                    }
                }
            }
            return new Model(iMod, doInit: false);
        }
        public string ModelDir()
        {
            return this.dir;
        }
        public static string RecentModelDir() { return RecentModel().dir; }

        internal void Handler()
        {
            wrToFile = ifWrToFile();
            if (!wrToFile) return;  // if model not changed -> no handing is necessary

            getGroups();
            foreach (var gr in elmGroups)
            {
                bool b = false;
                foreach (var rule in Rules)
                {
                    Mtch _match = new Mtch(gr, rule);
                    if (_match.ok == Mtch.OK.Match) { matches.Add(_match); b = true; break; }
//27/3                    else log.Info("No Match Group. mat= " + gr.mat + "\tprf=" + gr.prf);
                }
                if(!b) log.Info("No Match Group. mat= " + gr.mat + "\tprf=" + gr.prf);
            }
            getSuppliers();
            getPricing();
        }

        private void getPricing()
        {
            Log.set("Models.getPricing()");
            int cnt = 0;
            var elms = new Dictionary<string, Elm>();
            foreach (var elm in elements) elms.Add(elm.guid, elm);
            foreach (var mgr in elmMgroups)
            {
                foreach(string id in mgr.guids)
                {
                    mgr.totalPrice += elms[id].price;
                }
                cnt += mgr.guids.Count();
            }
            foreach(var elm in ElmAttSet.ElmAttSet.Elements)
            {

            }
            Log.exit();
        }

        /// <summary>
        /// getGroups() - groupping of elements of Model by Material and Profile
        /// </summary>
        /// <ToDo>30.9.2016 - попробовать перенести этот метод в ElmAttSet.Groups</ToDo>
        /// <history> 2016.09.29 </history>
        public void getGroups()
        {
            Dictionary<string, ElmAttSet.ElmAttSet> Elements = new Dictionary<string, ElmAttSet.ElmAttSet>();
            foreach (var elm in elements) { Elements.Add(elm.guid, elm); }

            //-- группы по Материалам elm.mat
            var matGroups = from elm in elements group elm by elm.mat;
            foreach (var matGr in matGroups)
            {
                List<string> guids = new List<string>();
                foreach (ElmAttSet.ElmAttSet element in matGr)
                {
                    //                      log.Info("mgr.mat=" + matGr.Key + " mgr.element.guid=" + element.guid);
                    guids.Add(element.guid);
                }
                ElmAttSet.Mgroup Mgr = new ElmAttSet.Mgroup(elements, matGr.Key, guids);
                elmMgroups.Add(Mgr);
            }
            log.Info("----- Material Groups Count = " + elmMgroups.Count + "\tfrom " + elements.Count + " elements ----");
            foreach (var mtGr in elmMgroups)            
                log.Info("material= " + mtGr.mat + "\tCount= " + mtGr.guids.Count + "\tвес=" + mtGr.totalWeight + "\tобъем=" + mtGr.totalVolume);
            
            //-- группы по Материалу и Профилю elm.mat && elm.prf
            this.elmGroups.Clear();
            foreach (var Mgr in elmMgroups)
            {
                string curMat = Mgr.mat;
                string curPrf = Elements[Mgr.guids[0]].prf;
                List<string> guids = new List<string>();
                foreach (var g in Mgr.guids)
                {
                    ElmAttSet.ElmAttSet elm = Elements[g];
                    if (elm.prf == curPrf) guids.Add(g);
                    else
                    {
                        this.elmGroups.Add(new ElmGr(Elements, curMat, curPrf, guids));
                        curPrf = elm.prf;
                        guids = new List<string>();
                        guids.Add(g);
                    }
                }
                if(guids.Count != 0) this.elmGroups.Add(new ElmGr(Elements, curMat, curPrf, guids));
            }
            log.Info("----- <Material, Profile> Groups Count = " + this.elmGroups.Count);
            int chkSum = 0;
            foreach(var gr in this.elmGroups)
            {
                log.Info("material= " + gr.mat + "\tprofile= " + gr.prf + "\tCount= " + gr.guids.Count);
                chkSum += gr.guids.Count;
            }
            log.Info("-------------- CheckSum: total elements count in all groups = " + chkSum);
        }
        public void setElements(List<ElmAttSet.ElmAttSet> els)
        {
            elements.Clear();
            foreach (var elm in els) elements.Add(elm);
        }
        public void setElements(Dictionary<string, Elm> els)
        {
            elements.Clear();
            foreach (var elm in els) elements.Add(elm.Value);
        }
        private void getSuppliers()
        {
//29.11            Suppliers = Supplier.Start(); //!! времянка - список всех Поставщиков
        }
        /// <summary>
        /// ifWrToFile() возвращает true, если модель изменилась относительно имеющейся в журнале записи о ней
        /// </summary>
        /// <returns></returns>
        private bool ifWrToFile()
        {
            //TODO 21/08/16 здесь вычислять MD5 прочитанной модели, сопоставлять результат с mod.MD5, если не совпадают - wrToFile = true;
            //              если wrToFile = false - больше ничего не делать 
            //////////////var controlSun = mod.elements.Sum(); для вычисления Sum надо реализовать IQuerible() с ComputeMD5()
            //////////////string md5 = mod.elements.MD5();    
            return true;    // пока не реализовано TODO - для отладки
        }

        internal void Report()
        {
            if (!wrToFile) return;

            wrModel(WR_MODEL_INFO);         // общая информация о модели: имя, директория, MD5 и др
            wrModel(WR_MODEL_RAW);          // элементы с атрибутами, как они прочитаны из модели
            wrModel(WR_MODEL_MATERIALS);    // сводка по материалам, их типам (бетон, сталь и др)
            wrModel(WR_MODEL_SUPPLIERS);    // сводка по поставщикам проекта (контакты, URL прайс-листа, закупки)
            wrModel(WR_MODEL_RULES);        // перечень Правил, используемых для обработки модели
            wrModel(WR_MODEL_REPORT);       // отчет-сопоставление групп <материал, профиль> c прайс-листами поставщиков      
        }
    } // end class Model
} // end namespace Model
