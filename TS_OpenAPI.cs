﻿/*-----------------------------------------------------------------------
 * TS_OpenAPI -- Interaction with Tekla Structure over Open API
 * 
 * 30.6.2016  Pavel Khrapkin, Alex Bobtsov
 *  
 *----- History ------------------------------------------
 3.1.2016 АБ получаем длину элемента
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
 * 30.6/2016 PKh - IsTeklaActive() modified
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
using System.Diagnostics;

using Tekla.Structures;
using TSD = Tekla.Structures.Dialog.ErrorDialog;
using Tekla.Structures.Model;

using Log = match.Lib.Log;
using Msg = TSmatch.Message.Message;
using Lib = match.Lib.MatchLib;
using TSM = Tekla.Structures.Model;
using Elm = TSmatch.ElmAttSet.ElmAttSet;

namespace TSmatch.Tekla
{
    class Tekla
    {
        const string MYNAME = "Tekla.Read v2.0";
        public enum ModelDir { exceldesign, model, macro, environment };
/* 21.6.2016 -- заменяем на ElmAttSet
        public struct AttSet : IComparable<AttSet>
        {
            public string guid, mat, mat_type, prf;
            public double lng, weight, volume;
            public AttSet(string g, string m, string mt, string p, double l, double w, double v)
            { guid = g; mat = m; mat_type = mt;  prf = p; lng = l; weight = w; volume = v; }

            public int CompareTo(AttSet att)
            {
                int result = mat.CompareTo(att.mat);
                if (result == 0) result = prf.CompareTo(att.prf);
                if (result == 0) return -lng.CompareTo(att.lng);
                return result;
            }
        }
        public class AttSetCompararer : IEqualityComparer<AttSet>
        {
            public bool Equals(AttSet p1, AttSet p2)
            {
                if (p1.guid == p2.guid & p1.mat == p2.mat & p1.prf == p2.prf & p1.lng == p2.lng
                    & p1.volume == p2.volume & p1.weight == p2.weight) return true;
                else return false;
            }
            public int GetHashCode(AttSet p)
            {
                int hCode = (p.guid + p.mat + p.prf + p.lng.ToString()
                    + p.volume.ToString() + p.weight.ToString()).GetHashCode();
                return hCode.GetHashCode();
            }
        } // class AttSetCompararer
        private static List<AttSet> ModAtr = new List<AttSet>();
21/6/2016 заменяем на ElmAttSet */
        public static TSM.ModelInfo ModInfo;

        public static string MyName = MYNAME;
////        public static string ModelMD5;

        public static List<Elm> Read()
        {
            Log.set("TS_OpenAPI.Read");
            Elm.Elements.Clear();
            TSM.Model model = new TSM.Model();
            List<Part> parts = new List<Part>();
            ModInfo = model.GetInfo();
            ModInfo.ModelName = ModInfo.ModelName.Replace(".db1", "");
            TSM.ModelObjectSelector selector = model.GetModelObjectSelector();
            System.Type[] Types = new System.Type[1];
            Types.SetValue(typeof(Part), 0);

            TSM.ModelObjectEnumerator objectList = selector.GetAllObjectsWithType(Types);
            int totalCnt = objectList.GetSize();
            var progress = new TSM.Operations.Operation.ProgressBar();
            bool displayResult = progress.Display(100, "TSmatch", "Reading model. Pass component records:", "Cancel", " ");
            int ii = 0;
            while (objectList.MoveNext())
            {
                TSM.Part myPart = objectList.Current as TSM.Part;
                if (myPart != null)
                {
                    ii++;
                    double lng = 0.0;
                    double weight = 0.0;
                    double vol = 0.0;
                    string guid = "";
                    string mat_type = "";
                    double price = 0.0;
                    //string profile = "";
                    //double width = 0.0, height = 0.0;
                    //myPart.GetReportProperty("PROFILE", ref profile);
                    //myPart.GetReportProperty("WIDTH", ref width);
                    //myPart.GetReportProperty("HEIGHT", ref height);
                    //myPart.GetReportProperty("WEIGHT_NET", ref weight);

                    myPart.GetReportProperty("GUID", ref guid);
                    myPart.GetReportProperty("LENGTH", ref lng);
                    myPart.GetReportProperty("WEIGHT", ref weight);
                    myPart.GetReportProperty("VOLUME", ref vol);
                    myPart.GetReportProperty("MATERIAL_TYPE", ref mat_type);

                    lng = Math.Round(lng, 0);
                    //string cut = "";
                    //myPart.GetReportProperty("CAST_UNIT_TYPE", ref cut);
                    //ModAtr.Add(new AttSet(myPart.Material.MaterialString,
                    //                      profile, lng, weight, vol));
                    //21/6/2016 в отладке                    Elm.Elements.Add(new Elm());
                    new Elm(guid, myPart.Material.MaterialString, mat_type,
                                           myPart.Profile.ProfileString,
                                           lng, weight, vol, price);
                    if (ii % 500 == 0) // progress update every 500th items
                    {
                        if (progress.Canceled())
                        {
//                            new Log("\n\n======= TSmatch pass model CANCEL!! =======  ii=" + ii);
//                            TSD.Show()
                            break;
                        }
                        progress.SetProgress(ii.ToString(), 100 * ii / totalCnt);
                    }
                }
            } //while
            progress.Close();
            Elm.Elements.Sort();
            Log.exit();
            return Elm.Elements;
        } // Read
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
                    try
                    {
                        ModInfo = model.GetInfo();
                        ok = model.GetConnectionStatus() && ModInfo.ModelName != "";
                    }
                    catch { Msg.W("isTeklaActive no model Connection"); }
                    break;
                }
            }
            Log.exit();
            return ok;
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
                ModInfo.ModelName = ModInfo.ModelName.Replace(".db1", "");
                ok = ModInfo.ModelName == name;
            }
            Log.exit();
            return ok;
        }
    } //class Tekla
} //namespace