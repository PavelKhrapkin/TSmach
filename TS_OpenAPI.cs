﻿/*-----------------------------------------------------------------------
 * TS_OpenAPI -- Interaction with Tekla Structure over Open API
 * 
 * 29.11.2017  Pavel Khrapkin, Alex Bobtsov
 *
 *----- ToDo ---------------------------------------------
 * - реализовать интерфейс IAdapterCAD, при этом избавится от static
 *----- History ------------------------------------------
 *  3.1.2016 АБ получаем длину элемента
 * 12.1.2016 PKh - добавлено вычисление MD5 по списку атрибутов модели, теперь это public string.
 *               - из имени модели удалено ".db1"
 * 14.1.2016 PKh - возвращаем в pulic string MyName версию этого метода
 * 21.1.2016 PKh - сортировка AttSet 
 * 25.1.2016 PKh - подсчет MD5 и проверку перенес в ModAtrMD5()
 *  5.2.2016 PKh - определяем путь к каталогу exceldesign в среде Tekla
 * 11.2.2016 PKh - Weight и volume атрибуты добавлены в AttSet
 * 19.2.2016 PKh - GetTeklaDir(ModelDir)
 *  4.3.2016 PKh - Add GUID in AttSet; "Fixed" profile is used
 *  6.3.2016 PKh - isTeklaActive() metod included
 * 10.3.2016 PKh - AttSet Compararer implemented
 * 20.4.2016 PKh - GetTeklaDir() rewritten
 *  4.6.2016 PKh - GetTeklaDir(Environment) add
 *  5.6.2016 PKh - isTeklaModel(name) add
 * 21.6.2016 PKh - ElmAttSet module keep all Elements instead of AttSet
 * 30.6.2016 PKh - IsTeklaActive() modified
 * 22.8.2016 PKh - Scale method to account unit in Model
 * 29.5.2017 PKh - Get Russian GOST profile from UDA
 *  7.9.2017 PKh - Read Embed objects
 * 18.9.2017 PKh - private ReadModObj() use
 * 28.9.2017 PKh - 9% acceleration with GetAllReportProperty
 * 26.10.2017 - HighLight cycle management with hlFlag
 * 19.11.2017 - HighLight - remove thread management as useless
 * 29.11.2017 - non-static Message adoption
 * -------------------------------------------
 * public Structure AttSet - set of model component attribuyes, extracted from Tekla by method Read
 *                           AttSet is Comparable, means Sort is applicable, and 
 *
 *      METHPDS:
 * Read()           - read current model from Tekla, return List<ElmAttSet> - list of this model attributes.
 *                    Model element atrtibutes conaines in class ElmAttSet:
 *                    * all Element Properties - in the class fields: Material, Profile, Guid, Volume, Weight etc
 *                    * TAG - propertiy names for getting them from Tekla Structures
 *                    * List<ElmAttSet>Elements - static list of properties all Elements
 * ModAtrMD5()      - calculate MD5 - contol sum of the current model
 * GetTeklaDir(mode) - return Path to the model directory, or Path to exceldesign in Tekla environmen
 * isTeklaActive()  - return true, when Tekla up and runing, else false
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures.Model.Operations;
//using Tekla.Structures.Drawing;

using Log = match.Lib.Log;
using TSM = Tekla.Structures.Model;
using Elm = TSmatch.ElmAttSet.ElmAttSet;
using Emb = TSmatch.EmbedAttSet.EmbedAttSet;
using System.Collections;
using Tekla.Structures;

namespace TSmatch.Tekla
{
    public class Tekla //: IAdapterCAD
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("Tekla:TS_OpenAPI");
        Message.Message Msg = new Message.Message();

        const string MYNAME = "Tekla.Read v2.3";

        public enum ModelDir { exceldesign, model, macro, environment };
        public static TSM.ModelInfo ModInfo;
        public static string MyName = MYNAME;
        ////        public static string ModelMD5;

        ////public List<Elm> Read(string modName) { return Read(); }
        ////public List<Elm> Read(this TSmatch.Model.Model _mod ) { return Read(_mod.name); }
        protected TSM.Model model = new TSM.Model();
        protected TSM.ModelObjectSelector selector;
        /// <summary>
        /// parts - Dictionary of all Parts in Model with GUID as a Key
        /// </summary>
        protected Dictionary<string, TSM.Part> dicParts = new Dictionary<string, TSM.Part>();

        public Tekla() { }

        #region --- Read Model area ---
        public List<Elm> Read(string dir = "", string name = "")
        {
            Log.set("TS_OpenAPI.Read");
            List<Elm> elements = new List<Elm>();
            ModInfo = model.GetInfo();
            if (dir != "" && ModInfo.ModelPath != dir
                || name != "" && ModInfo.ModelName != String.Concat(name, ".db1")) Msg.F("Tekla.Read: Another model loaded, not", name);
            ModInfo.ModelName = ModInfo.ModelName.Replace(".db1", "");
 
            dicParts = ReadModObj<TSM.Part>();

            //20/11/17            ArrayList part_string = new ArrayList() { "MATERIAL", "MATERIAL_TYPE", "PROFILE" };
            //20/11/17            ArrayList part_double = new ArrayList() { "LENGTH", "WEIGHT", "VOLUME" };
            //20/11/17            ArrayList part_int = new ArrayList();
            //20/11/17            Hashtable all_val = new Hashtable();

            foreach (var part in dicParts)
            {
                Elm elm = new Elm();
                elm.mat = part.Value.Material.MaterialString;
                elm.prf = part.Value.Profile.ProfileString;
                part.Value.GetReportProperty("LENGTH", ref elm.length);
                part.Value.GetReportProperty("WEIGHT", ref elm.weight);
                part.Value.GetReportProperty("VOLUME", ref elm.volume);
                part.Value.GetReportProperty("MATERIAL_TYPE", ref elm.mat_type);

                //////////////////double lng = 0, weight = 0, vol = 0;
                //////////////////var p = part.Value;
                //////////////////p.GetReportProperty("LENGTH", ref lng);
                //////////////////p.GetReportProperty("WEIGHT", ref weight);
                //////////////////p.GetReportProperty("VOLUME", ref vol);
                //////////////////elm.volume = vol;
                //////////////////elm.weight = weight;
                //////////////////elm.length = lng;
                //20/11/17                part.Value.GetAllReportProperties(part_string, part_double, part_int, ref all_val);
                //20/11/17               elm.mat = (string)all_val[part_string[0]];
                //20/11/17elm.mat_type = (string)all_val[part_string[1]];
                //20/11/17               elm.prf = (string)all_val[part_string[2]];
                //20/11/17  elm.length = (double)all_val[part_double[0]];
                //20/11/17elm.weight = (double)all_val[part_double[1]];
                //20/11/17elm.volume = (double)all_val[part_double[2]];
                elm.guid = part.Key;
                elements.Add(elm);
            }
            Scale(elements);
            elements.Sort();
            Log.exit();
            return elements;
        } // Read

        protected Dictionary<string, T> ReadModObj<T>() where T : ModelObject
        {
            var result = new Dictionary<string, T>();
            selector = model.GetModelObjectSelector();
            Type[] Types = new Type[1];
            Types.SetValue(typeof(T), 0);
            ModelObjectEnumerator objParts = selector.GetAllObjectsWithType(Types);
            int totalCnt = objParts.GetSize();
            var progress = new Operation.ProgressBar();
            bool displayResult = progress.Display(100, "TSmatch", "Reading model. Pass component records:", "Cancel", " ");
            int iProgress = 0;
            while(objParts.MoveNext())
            {
                T myObj = objParts.Current as T;
                if (myObj == null) continue;
                string guid = string.Empty;
                myObj.GetReportProperty("GUID", ref guid);
                result.Add(guid, myObj);
                iProgress++;
                if (iProgress % 500 == 0)
                {
                    progress.SetProgress(iProgress.ToString(), 100 * iProgress / totalCnt);
                    if (progress.Canceled()) break;
                }
            }
            progress.Close();
            return result;
        }

        static void Scale(List<Elm> elements)
        {
            foreach (var elm in elements)
            {
                // weight [kg]; volume [mm3]->[m3]
                elm.volume = elm.volume / 1000 / 1000 / 1000;     // elm.volume [mm3] -> [m3] 
            }
        }

        public List<Elm> Read(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            return Read(dir, name);
        }
#endregion --- Read area ---

        public bool IfLockedWait(string FileName)
        {
            // try 10 times
            int RetryNumber = 20;
            while (true)
            {
                try
                {
                    using (FileStream FileStream = new FileStream(
                    FileName, FileMode.Open,
                    FileAccess.ReadWrite, FileShare.ReadWrite))
                    {
                        byte[] ReadText = new byte[FileStream.Length];
                        FileStream.Seek(0, SeekOrigin.Begin);
                        FileStream.Read(ReadText, 0, (int)FileStream.Length);
                    }
                    return true;
                }
                catch (IOException)
                {
                    // wait one second
                    Thread.Sleep(500);
                    RetryNumber--;
                    if (RetryNumber == 0)
                        return false;
                }
            }
        }

        public void WriteToReport(string path)
        {
            Operation.CreateReportFromAll("ОМ_Список", path, "MyTitle", "", "");
            while (!File.Exists(path)) Thread.Sleep(500);
            if (!IfLockedWait(path)) Msg.F("No Tekla Report created");
        }

        public string ReadReport(string path)
        {
            String rep = string.Empty;
            try
            {
                using (StreamReader sr = new StreamReader(path))
                rep = sr.ReadToEnd();
            }
            catch (Exception e) { Msg.F("Tekla ReadReport: can't read file: " + e.Message); }
            return rep;
        }

        public int elementsCount()
        {
            Log.set("TS_OpenAPI.elementsCount()");
            TSM.ModelObjectSelector selector = model.GetModelObjectSelector();
            System.Type[] Types = new System.Type[1];
            Types.SetValue(typeof(TSM.Part), 0);
            TSM.ModelObjectEnumerator objectList = selector.GetAllObjectsWithType(Types);
            Log.exit();
            int totalCnt = objectList.GetSize();
            return totalCnt;
        }

        #region ---- HighLight ----
        /// <summary>
        /// HeghlightElements(List<Elm>elements, color) - change color of elements in list 
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="color"></param>

        private List<TSM.ModelObject> colorObjects;

        public void HighlightElements(Dictionary<string, Elm> els, int color = 1)
        {
            DateTime t0 = DateTime.Now;
            Log.set("TS_OpenAPI.HighLightElements");
            colorObjects = new List<TSM.ModelObject>();
            foreach (var elm in els)
            {
                Identifier id = new Identifier(elm.Key);
                var obj = model.SelectModelObject(id);
                colorObjects.Add(obj);
            }
            DateTime t1 = DateTime.Now;
            var _color = new Color(0.0, 0.0, 1.0);
            ModelObjectVisualization.SetTransparencyForAll(TemporaryTransparency.SEMITRANSPARENT);
            ModelObjectVisualization.SetTemporaryState(colorObjects, _color);
            DateTime t2 = DateTime.Now;
            string timing = string.Format("=== HighLight fill colorObject t={0}, Tekla t={1}", t1 - t0, t2 - t1);
            Log.exit();
        }

        public void HighlightClear()
        {
            ModelObjectVisualization.SetTransparencyForAll(TemporaryTransparency.VISIBLE);
        }
        #endregion ---- HighLight ----
        /*2016.6.21        /// <summary>
                /// ModAtrMD5() - calculate MD5 of the model read from Tekla in ModAtr
                /// </summary>
                /// <remarks>It could take few minutes for the large model</remarks>
                public static string ModAtrMD5()
                {
                    //            DateTime t0 = DateTime.Now;  
                    string str = "";
                    foreach (var att in ModAtr) str += att.mat + att.prf + att.lng.ToString();
                    ModelMD5 = Lib.ComputeMD5(str);
                    return ModelMD5;
                    //            new Log("MD5 time = " + (DateTime.Now - t0).ToString());
                } // ModAtrMD5
        2016.6.21 */
        public static string GetTeklaDir(ModelDir mode)
        {
            string TSdir = "";
            switch (mode)
            {
                case ModelDir.exceldesign:
                    TeklaStructuresSettings.GetAdvancedOption("XS_EXTERNAL_EXCEL_DESIGN_PATH", ref TSdir);
                    break;
                case ModelDir.model:
                    TSM.Model model = new TSM.Model();
                    ModInfo = model.GetInfo();
                    TSdir = ModInfo.ModelPath;
                    break;
                case ModelDir.macro:
                    TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref TSdir);
                    string[] str = TSdir.Split(';');
                    TSdir = str[0] + @"\modeling";     // this Split is to ignore other, than common TS Enviroments
                    break;
                case ModelDir.environment:
                    TSdir = GetTeklaDir(ModelDir.exceldesign);
                    TSdir = Path.Combine(TSdir, @"..\..");
                    TSdir = Path.GetFullPath(TSdir);
                    break;
            }
            //////////            var ff = TeklaStructuresInfo.GetCurrentProgramVersion();
            //////////            var dd = TeklaStructuresFiles.GetAttributeFile(TSdir);
            //////////            TSdir = TS.TeklaStructuresFiles();
            return TSdir;
        }
        /// <summary>
        /// IsTeklaActice() - return true if TeklaStructures Process exists in Windows, and model is available 
        /// </summary>
        /// <returns>true if Tekla is up and running</returns>
        public static bool isTeklaActive()
        {
            Log.set("isTeklaActive()");
            bool ok = false;
            const string Tekla = "TeklaStructures";
            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.ToLower().Contains(Tekla.ToLower()))
                {
                    TSM.Model model = new TSM.Model();
                    if (!model.GetConnectionStatus()) goto error;
                    try
                    {
                        ModInfo = model.GetInfo();
                        ok = model.GetConnectionStatus() && ModInfo.ModelName != "";
                    }
                    catch { goto error; }
                    break;
                }
            }
            Log.exit();
            return ok;

            error: throw new Exception("isTeklaActive no model Connection");
        }
        /// <summary>
        /// isTeklaModel(name) -- check if Tekla open with the model name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>true if in Tekla active and open model == name</returns>
        /// <history>5.6.2016</history>
        public static bool isTeklaModel(string name)
        {
            Log.set("TS_OpenAPI.isTeklaModel(\"" + name + "\")");
            bool ok = false;
            if (isTeklaActive())
            {
                TSM.Model model = new TSM.Model();
                ModInfo = model.GetInfo();
                name = Path.GetFileNameWithoutExtension(name);
                //                ModInfo.ModelName = ModInfo.ModelName.Replace(".db1", "");
                string inTS = Path.GetFileNameWithoutExtension(ModInfo.ModelName);
                ok = Path.GetFileNameWithoutExtension(ModInfo.ModelName) == name;
            }
            Log.exit();
            return ok;
        }
        public static string getModInfo()
        {
            string result = String.Empty;
            TSM.Model model = new TSM.Model();
            ModInfo = model.GetInfo();
            result = Path.GetFileNameWithoutExtension(ModInfo.ModelName);
//            if (!isTeklaModel(result)) Msg.F("TS_Open API getModInfo error");
            return result;
        }

        public List<Emb> ReadCustomParts()
        {
            List<Emb> result = new List<Emb>();
  //          TSM.Model model = new TSM.Model();
            TSM.ModelObjectSelector selector = this.model.GetModelObjectSelector();
            System.Type[] Types = new System.Type[1];
            Types.SetValue(typeof(Part), 0);
            TSM.ModelObjectEnumerator objectList = selector.GetAllObjectsWithType(Types);
            List<Part> parts = new List<Part>();
            while (objectList.MoveNext())
            {
                TSM.Part myPart = objectList.Current as TSM.Part;
                if (myPart == null) continue;
                if (myPart.Class != "100" && myPart.Class != "101") continue;
                parts.Add(myPart);
                if (myPart.Name.Contains("SBKL")) continue;
 //               var project_code = myPart.GetUserProperty("j_fabricator_name", ref vendorName);
            }
            return result;
        }

        /// <summary>
        /// PartsInAssembly - Return List of GUIDs of Parts in assembly or empty list, if Part is not Assembly Father
        /// </summary>
        public List<string> PartsInAssembly(string guid)
        {
            const string me = "Tekla__PartsInAssembly_";
            ////if (parts.Count == 0) Msg.F(me + "PartList_Not_Initialized");
            ////Part myPart = parts.Find(x => x.)
            ////if (!parts.Contains(myPart)) Msg.F(me + "Part_Not_In_Model");
            List<string> guids = new List<string>();
            string myPartGuid = string.Empty;
////            myPart.GetReportProperty("GUID", ref myPartGuid);

            return guids;
        }
#if NOT_READY_YET
        public void Example1()
        {
            DrawingHandler MyDrawingHandler = new DrawingHandler();
            ViewBase _view = MyDrawingHandler.GetActiveDrawing().GetSheet().GetAllViews().Current as ViewBase;

            EmbeddedObjectAttributes attributes = new EmbeddedObjectAttributes();
            attributes.XScale = 0.5;
            attributes.YScale = 0.5;
            DwgObject dxf = new DwgObject(_view, new Point(100, 100),
                                          Path.GetFullPath("my_dxf.dxf"), attributes);
            dxf.Insert();
        }
#endif
    } //class Tekla
} //namespace