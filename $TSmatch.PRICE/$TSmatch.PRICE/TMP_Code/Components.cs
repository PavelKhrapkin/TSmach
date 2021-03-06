﻿/*----------------------------------------------------------------------------
 * Components -- работа с документами - прайс-листами поставщиков компонентов
 * 
 * 28.02.2017  П.Храпкин
 *
 *----- ToDo -----
 * 29.12.2016 написать загрузку из прайс-листа setComp(..) c разбором LoadDescriptor
 * --- журнал ---
 * 30.11.2016 made as separate module, CompSet is now in another file
 * 30.12.2016 fill matFP, prfFP in setComp()
 * 28.02.2017 fill component fields incliding List<FP> from price-list in constructor
 * ---------------------------------------------------------------------------
 *      МЕТОДЫ:
 * getCompSet(name, Supplier) - getCompSet by  its name in Supplier' list
 * setComp(doc) - инициальзация данных для базы компонентов в doc
 * getComp(doc) - загружает Excel файл - список комплектующих от поставщика
 * UddateFrInternet() - обновляет документ комплектующих из Интернет  
 * ----- class CompSet
 *      МЕТОДЫ:
 * getMat ()    - fill mat ftom CompSet.Components and Suplier.TOC
 * 
 *    --- Вспомогательные методы - подпрограммы ---
 * UpgradeFrExcel(doc, strToDo) - обновление Документа по правилу strToDo
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using log4net;

using TST = TSmatch.Test.assert;
using Lib = match.Lib.MatchLib;
using Log = match.Lib.Log;
using Msg = TSmatch.Message.Message;
using Decl = TSmatch.Declaration.Declaration;
using TS = TSmatch.Tekla.Tekla;
using Docs = TSmatch.Document.Document;
using Mod = TSmatch.Model.Model;
using Mtch = TSmatch.Matcher.Mtch;
using Supl = TSmatch.Suppliers.Supplier;
using FP = TSmatch.FingerPrint.FingerPrint;
using TypeFP = TSmatch.FingerPrint.FingerPrint.type;
using TSmatch.Document;
using TSmatch.Rule;
using Sec = TSmatch.Section.Section;
using SType = TSmatch.Section.Section.SType;
using ParType = TSmatch.Parameter.Parameter.ParType;
using TSmatch.ElmAttSet;

namespace TSmatch.Component
{
    public class Component
    {
        public static readonly ILog log = LogManager.GetLogger("Component");

        public readonly Dictionary<SType, FP> fps = new Dictionary<SType, FP>();

#if DEBUG   //-- 30-Mar-2017 -- вариант конструктора только для тестирования
        public Component(Dictionary<SType, string> comp
            , Dictionary<SType, FP> cs_fps)
        {
            bool flag = false;
            foreach (var fpD in cs_fps)
            {
                FP csFP = fpD.Value;
                FP compFP = new FP();
                string str = comp[fpD.Key];
                Sec s = new Sec(str);
                string ld = csFP.parN();
                if (ld.Contains('*'))       // * template
                {
                    compFP.pars = s.secPars(ld);
//30/3                    s.txs = 
                    break;
                }
                if (ld.Contains('{'))       // {2} Col() Parameter       
                {
                    compFP.pars.Add(new Parameter.Parameter(ld));
                    break;
                }
                fps.Add(fpD.Key, compFP);
            }
        } 
        // 30/3 ////////////////public Component(string description, string mat = ""
        ////////////////////////    , double length = 0, double weight = 0, double price = 0
        ////////////////////////    , Dictionary<SType,FP> csFPs = null)
        ////////////////////////void AddPar(SType stype, dynamic obj, FP csFP = null)
        ////////////////////////{
        ////////////////////////    if(csFP != null)
        ////////////////////////    {
        //////30/3//////////////        var sec = new Section.Section((string)obj);
        ////////////////////////        List<Parameter.Parameter> ps = sec.secPars(csFP.parN());
        ////////////////////////        if(ps.Count > 0) return;
        ////////////////////////    }
        ////////////////////////    if (    obj.GetType() == typeof(string) && obj != string.Empty
        ////////////////////////        || obj.GetType() == typeof(double) && (double)obj != 0)
        ////////////////////////    {
        ////////////////////////        fps.Add(stype, new FP(stype, obj));
        ////////////////////////    }
        ////////////////////////}
#endif  // DEBUG -- вариант для тестирования
        /// <summary>
        /// constructor Component(doc, i, List<FP>cs_fps, List<FP>rule_fps) - get Component from price-list in doc line i
        /// </summary>
        /// <param name="doc">document - price-list</param>
        /// <param name="i">line number in doc</param>
        /// <param name="cs_fps">FP of CompSet</param>
        public Component(Docs doc, int i, Dictionary<SType, FP> cs_fps)
        {
            bool flag = false;
            foreach (var fpD in cs_fps)
            {
                FP csFP = fpD.Value;
                string str = csFP.Int() == -1? "": doc.Body.Strng(i, csFP.Col());
                FP compFP = new FP(str, csFP, out flag);
                if (flag) fps.Add(csFP.section.type, compFP);
            }
            //////////////////////string[] sections = Lib.ToLat(doc.LoadDescription).ToLower().Split(';');
            //////////////////////bool flag = false;
            //////////////////////foreach (string sec in sections)
            //////////////////////{
            ///// 24/3 ///////////    if (string.IsNullOrEmpty(sec)) continue;                                 
            //////////////////////    ////////////////////////FP csFP = cs_fps.Find(x => x.section == x.RecognyseSection(sec));
            //////////////////////    ////////////////////////if (csFP == null) Msg.F("Component constructor: no CompSet.FP with doc.LoadDescription", sec);
            //////////////////////    /////// 7/7/2017 ///////int col = csFP.Col();
            //////////////////////    ////////////////////////string str = doc.Body.Strng(i, col);
            //////////////////////    ////////////////////////FP compFP = new FP(str, csFP, out flag);
            //////////////////////    ////////////////////////if (flag) fps.Add(compFP);
            //////////////////////}
        }

        public string viewComp(SType stype)
        {
            string str = "";
            FP fp;
            try { fp = fps[stype]; }
            catch { return string.Empty; }
            int i = 0;
            foreach (Parameter.Parameter p in fp.pars)
            {
                if (i++ > 1) str += "x";
                str += p.par.ToString();
            }
            return str;
        }
        ///////////////////// <summary>
        ///////////////////// setComp(doc) - fill price list of Components from doc
        ///////////////////// setComp(doc_name) - overload
        ///////////////////// </summary>
        ///////////////////// <param name="doc">price-list</param>
        ///////////////////// <returns>List of Components</returns>
        ///////////////////// <history>26.3.2016
        /////////////////////  3.4.2016 - setComp(doc_name) overload
        /////////////////////  8.4.2016 - remove unnecteesary fields - bug fix
        ///////////////////// 14.4.2016 - field mat = Material fill 
        /////////////////////  </history>
        //////////////////public static List<Component> setComp(string doc_name)
        //////////////////{ return setComp(Docs.getDoc(doc_name)); }
        //////////////////public static List<Component> setComp(Docs doc)
        //////////////////{
        //////////////////    Log.set("setComp(" + doc.name + ")");
        //////////////////    List<int> docCompPars = Lib.GetPars(doc.LoadDescription);
        //////////////////    //-- заполнение массива комплектующих Comps из прайс-листа металлопроката
        //////////////////    List<Component> Comps = new List<Component>();
        //////////////////    for (int i = doc.i0; i <= doc.il; i++)
        //////////////////    {
        //////////////////        try
        //////////////////        {
        //////////////////            string descr = doc.Body.Strng(i, docCompPars[0]);
        //////////////////            double lng = 0;
        //////////////////            //-- разбор параметров LoadDescription

        //////////////////            List<int> strPars = Lib.GetPars(descr);
        //////////////////            string docDescr = doc.LoadDescription;
        //////////////////            int parShft = 0;
        //////////////////            while (docDescr.Contains('/'))
        //////////////////            {
        //////////////////                string[] s = doc.LoadDescription.Split('/');
        //////////////////                List<int> c = Lib.GetPars(s[0]);
        //////////////////                int pCol = c[c.Count() - 1];    // колонка - последний параметр до '/'
        //////////////////                List<int> p = Lib.GetPars(s[1]);
        //////////////////                lng = strPars[p[0] - 1];    // длина заготовки = параметр в str; индекс - первое число после '/'
        // 29/3/2017 /////                docDescr = docDescr.Replace("/", "");
        //  устарело /////                parShft++;
        //////////////////            }
        //////////////////            if (lng == 0)
        //////////////////                lng = doc.Body.Int(i, docCompPars[1]) / 1000;    // для lng указана колонка в LoadDescription   
        //////////////////            double price = doc.Body.Double(i, docCompPars[2] + parShft);
        //////////////////            double wgt = 0.0;   //!!! времянка -- пока вес будем брать только из Tekla
        //////////////////            string mat = "";    //!!! времянка -- материал нужно извлекать из description или описания - еще не написано!
        //////////////////            Comps.Add(new Component(descr, mat, lng, wgt, price));
        //////////////////        }
        //////////////////        catch { Msg.F("Err in setComp", doc.name); }
        //////////////////    }
        //////////////////    Log.exit();
        //////////////////    return Comps; 
        //////////////////}

        public bool isMatch(ElmAttSet.Group gr, Rule.Rule rule = null) //25/3 Dictionary<SecTYPE,List<string>> ruleFPs = null)
        {
            if (!isMatchGrRule(SType.Material, gr, rule)) return false;
            if (!isMatchGrRule(SType.Profile,  gr, rule)) return false;
            return true;
        }

        bool isMatchGrRule(SType stype, ElmAttSet.Group gr, Rule.Rule rule)
        {
            if (rule == null || !fps.ContainsKey(stype)) return true;
            var ruleSyns = rule.synonyms;
            string comMatPrf = fps[stype].pars[0].par.ToString();
            string grMatPrf = stype == SType.Material ? gr.mat : gr.prf;
            if( grMatPrf == comMatPrf ) return true;
            if (ruleSyns != null && ruleSyns.ContainsKey(stype))
            {
                List<string> Syns = ruleSyns[stype].ToList();
                if (!Lib.IContains(Syns, comMatPrf) || !Lib.IContains(Syns, grMatPrf)) return false;

                string c = strExclude(comMatPrf, Syns);
                string g = strExclude(grMatPrf, Syns);
//27/3                if(c == g) return true;
                return c.Contains(g);
                ////////////////var p1 = Params(Syns, comMatPrf, );
                //// 27/3 //////var p2 = Params(Syns, grMatPrf);
                ////////////////bool b = p1 != p2  && stype == SType.Material;

                return Params(Syns, comMatPrf) == Params(Syns, grMatPrf);
            }
            return false;
        }

        private string strExclude(string str, List<string> syns)
        {
            foreach(string s in syns)
            {
                if (!str.Contains(s)) continue;
                return str.Substring(s.Length);
            }
            Msg.F("Rule.strExclude error", str, syns);
            return null;
        }

        string Params(List<string> lst, string str)
        {
            foreach (string st in lst)
            {
                if (!str.Contains(st)) continue;
                string result = str.Substring(st.Length);
                return str.Substring(st.Length);
            }
            return null;
        }

        /// <summary>
        /// getComp(CompSet cs) - загружает Документ - прайс-лист комплектующих
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        /// <history> 21.2.2016
        /// 24.2.2016 - выделил в отдельный модуль Components
        /// 27.2.2016 - оформил внутреннюю структуру Component и встроил в getComp(doc)
        ///             для разбора, где указана длина комплектующего, используем строку
        ///             вида <col>/<№ параметра в str> 
        /// </history>
        public void getComp()
        {
            Log.set("getComp");
//            setComp(this.doc);
            try
            {

            }
            catch
            {

            }


            ////List<int> docCompPars = Mtch.GetPars(doc.LoadDescription);
            //////-- заполнение массива комплектующих Comps
            ////Comps.Clear();
            ////for (int i = doc.i0; i <= doc.il; i++)
            ////{
            ////    string str = doc.Body.Strng(i, docCompPars[0]);
            ////    double lng = 0;
            ////    //-- разбор параметров LoadDescription
            ////    List<int> strPars = Mtch.GetPars(str);
            ////    string docDescr = doc.LoadDescription;
            ////    int parShft = 0;
            ////    while (docDescr.Contains('/'))
            ////    {
            ////        string[] s = doc.LoadDescription.Split('/');
            ////        List<int> c = Mtch.GetPars(s[0]);
            ////        int pCol = c[c.Count() - 1];    // колонка - последний параметр до '/'
            ////        List<int> p = Mtch.GetPars(s[1]);         
            ////        lng = strPars[p[0] - 1];    // длина заготовки = параметр в str; индекс - первое число после '/'
            ////        docDescr = docDescr.Replace("/", "");
            ////        parShft++;
            ////    }
            ////    if (lng == 0)
            ////        lng = doc.Body.Int(i, docCompPars[1])/1000;    // для lng указана колонка в Comp    
            ////    double? price = doc.Body.Double(i, docCompPars[2] + parShft);
            ////    double wgt = 0.0;   //!!! времянка -- пока вес будем брать из Tekla 
            ////    Comps.Add(new Component(str, lng, wgt, price));
            ////}
            Log.exit();
        }
        public static Docs UpgradeFrExcel(Docs doc, string strToDo)
        {
            Log.set("UpgradeFrExcel(" + doc.name + ", " + strToDo + ")");
            if (strToDo != "DelEqPar1") Log.FATAL("не написано!");
//!!            List<string> Comp = getComp(doc);
            //////int i = doc.i0;
            //////foreach (string s in Comp)
            //////{
            //////    string str = Lib.ToLat(s);
            //////    List<int> pars = Mtch.GetPars(s);
            //////    if (pars[0] == pars[1])
            //////    {        
            //////        string toDel = pars[0].ToString() + " x ";
            //////        str = str.Replace(toDel + toDel, toDel); 
            //////    }
            //////    doc.Body[i++, 1] = str;
            //////}
            //////doc.isChanged = true;
            //////Docs.saveDoc(doc);

            //for (int i = doc.i0, iComp = 0; i <= doc.il; i++)
            //{
            //    // doc.Body.Strng(i, 1) = Copm;
            //}
            Log.exit();
            return doc;
        }

////////////////////////        #region ------ test Component -----
////////////////////////#if DEBUG
////////////////////////        public static void testComponent()
////////////////////////        {
////////////////////////            Log.set("testComponent");
////////////////////////            //-- test environment preparation: set csFPs and doc - price list "ГК Монолит"
////////////////////////            Docs doc = Docs.getDoc("ГК Монолит");
////////////////////////            List<FP> csFPs = new List<FP>();
////////////////////////            Rule.Rule rule = new Rule.Rule(15);
//////////////////////////20/3            csFPs = rule.Parser(FP.type.CompSet, doc.LoadDescription);
////////////////////////            TST.Eq(csFPs.Count, 3);

////////////////////////            //-- simple Component line "B20" in col 1
////////////////////////            Component comp = new Component(doc, 8 + 3, csFPs);
////////////////////////            TST.Eq(comp.fps[0].txs[0], "b");
////////////////////////            TST.Eq(comp.fps[0].txs.Count, 1);
////////////////////////            TST.Eq(comp.fps[0].pars[0].ToString(), "20");
////////////////////////            TST.Eq(comp.fps[0].pars.Count, 1);
////////////////////////            //////////////////////var bb = SecTYPE.Material;
////////////////////////            //////////////////////var aa = Section.Section.type.Material;
////// 24/3 ////////////            //////////////////////bool comp.fps[0].section == SecTYPE.Material;
////////////////////////            ////// 7/3/2017 //////SecTYPE comp.fps[0].section 
////////////////////////            //////////////////////TST.Eq(comp.fps[0].section == FP.Section.Material, true);
////////////////////////            //////////////////////TST.Eq(comp.fps[1].section == FP.Section.Description, true);
////////////////////////            //////////////////////TST.Eq(comp.fps[1].pars.Count, 1);
////////////////////////            //////////////////////TST.Eq(comp.fps[2].txs[0].ToString(), "");
////////////////////////            //////////////////////TST.Eq(comp.fps[2].section == FP.Section.Price, true);
////////////////////////            doc.Close();

////////////////////////            //-- Уголок равнополочный Стальхолдинг -- разбор LoadDescription L{1}x{1}
////////////////////////            doc = Docs.getDoc("Уголок Стальхолдинг");
////////////////////////            rule = new Rule.Rule(4);
//////////////////////////20/3            csFPs = rule.Parser(FP.type.CompSet, doc.LoadDescription);
////////////////////////            TST.Eq(csFPs.Count, 3);
////////////////////////            TST.Eq(csFPs[0].pars[0], 1);

////////////////////////            comp = new Component(doc, 42, csFPs);
////////////////////////            TST.Eq(comp.fps[0].txs[0], "");
////////////////////////// 6/3/17   TST.Eq(comp.fps[0].pars[0], "");

////////////////////////            Log.exit();
////////////////////////        }
////////////////////////#endif //#if DEBUG
////////////////////////        #endregion ------ test Component ------
    } // end class Component
} // end namespace Component
