﻿/*-----------------------------------------------------------------------------------
 * SavedReport -- class for handle saved reports in TSmatchINFO.xlsx
 * 
 *  5.06.2017 П.Л. Храпкин
 *  
 *--- Unit Tests ---
 * UT_SavedReport_Raw 2017.05.27 11 sec
 *--- History  ---
 * 17.04.2017 выделен из модуля Model
 *  1.05.2017 with Document Reset and ReSave
 *  7.05.2017 написал SetFrSavedModelINFO(), переписал isReportConsystant()
 * 27.05.2017 - XML read and write model.elements as Raw.xml in Raw() 
 *  5.06.2017 - bug fix in SetFrSavedModel - recoursive call after Reset
 *--- Methods: -------------------      
 * bool GetSavedReport()    - read TSmatchINFO.xlsx, set it as a current Model
 *                            return true if name, dir, quantity of elements is
 *                            suit to the current model
 * IsModelCahanged - проверяет, изменилась ли Модель относительно сохраненного MD5
 ! lngGroup(atr)   - группирует элементы модели по парам <Материал, Профиль> возвращая массивы длинны 
 * Save(Model mod) - Save model mod in file TSmatchINFO.xlsx
 */
using log4net;
using System;
using System.IO;
using System.Collections.Generic;

using Log = match.Lib.Log;
using Lib = match.Lib.MatchLib;
using FileOp = match.FileOp.FileOp;
using Msg = TSmatch.Message.Message;
using Decl = TSmatch.Declaration.Declaration;
using Docs = TSmatch.Document.Document;
using Elm = TSmatch.ElmAttSet.ElmAttSet;
using Mod = TSmatch.Model.Model;
using TS = TSmatch.Tekla.Tekla;

namespace TSmatch.SaveReport
{
    public class SavedReport : Mod
    {
        public static readonly ILog log = LogManager.GetLogger("SavedReport");

        string sINFO = Decl.TSMATCHINFO_MODELINFO;
        string sRep = Decl.TSMATCHINFO_REPORT;
        string sRul = Decl.TSMATCHINFO_RULES;
        Docs dINFO, dRaw, dRep, dRul;
        private Mod ModelInCad;

        public void GetSavedReport(Mod mod)
        {
            Log.set("SR.GetSavedReport(\"" + mod.name + "\")");
            bool check = true;
            while (check)
            {
                if (dINFO == null && !TS.isTeklaActive()) Msg.F("SavedReport doc not exists and no CAD");
                dINFO = Docs.getDoc(sINFO);
                if (dINFO == null || dINFO.il < 9) { Reset(Decl.TSMATCHINFO_MODELINFO); continue; }
                SetSavedMod(mod);
                if (isChangedStr(ref mod.name, dINFO, 2, 2)) { ChangedModel(); continue; }
                if (isChangedStr(ref mod.dir, dINFO, 3, 2)) { Reset(sINFO); continue; }
                if (isChangedStr(ref mod.MD5, dINFO, 6, 2)) { ChangedModel(); continue; }
                if (isChangedStr(ref mod.pricingMD5, dINFO, 9, 2)) { ChangedPricing(); continue; }
                pricingDate = Lib.getDateTime(dINFO.Body.Strng(8, 2));

                elements = Raw(mod);
                if (elements == null && !TS.isTeklaActive()) Msg.F("No Saved elements in TSmatchINFO.xlsx");

                mh.getGroups(elements);
                elmGroups = mh.elmGroups;
                elmMgroups = mh.elmMgroups;
                Log.Trace("*SR.elements=", elements.Count, " gr=", elmGroups.Count);
                if (!Docs.IsDocExists(sRep)) { Reset(sRep); continue; }
                getSavedGroups();
                if (!Docs.IsDocExists(sRul)) { Reset(sRul); continue; }
                check = false;
            }
            Log.exit();
        }

        private void SetSavedMod(Mod mod)
        {
            Log.set("SetSavedReport");
            ModelInCad = mod;

            dINFO = Docs.getDoc(Decl.TSMATCHINFO_MODELINFO, fatal: false);
            dRep = Docs.getDoc(Decl.TSMATCHINFO_REPORT, fatal: false);

            name = mod.name;
            dir = mod.dir;
            phase = mod.phase;
            date = Lib.getDateTime(dINFO.Body.Strng(Decl.MODINFO_DATE_R, 2));
            made = mod.made; MD5 = mod.MD5;
            elementsCount = mod.elementsCount;
            pricingDate = mod.pricingDate;
            pricingMD5 = mod.pricingMD5;
            mh = mod.mh;

            Log.TraceOn();
            if (TS.isTeklaActive()) Log.Trace("Tekla active");
            else Log.Trace("No Tekla");
            Log.Trace("name =", name);
            Log.Trace("dir  =", dir);
            Log.Trace("phase=", phase);
            Log.Trace("made =", made);
            Log.Trace("date =", date);
            Log.Trace("prcDT=", pricingDate);
            Log.Trace("elCnt=", elementsCount);
            Log.Trace("strRl=", strListRules);
            Log.TraceOff();
            Log.exit();
        }
#if OLD
            if (isReportConsistent()) return;

            // сюда мы вообще-то не должны приходить - Recovery или Fatal происходят при проверке isReportConsistent()

            TS ts = new TS();
            //ToDo 21/4/17: когда буду делать САПР помимо Tekla, здесь переписать!
            if (!TS.isTeklaActive()) Msg.F("SavedReport inconsistant and no Tekla");
            name = TS.getModInfo();
            dir = TS.ModInfo.ModelPath;
            Mod m = mj.getModJournal(name, dir);
            date = m.date;
            dINFO = Docs.getDoc(Decl.TSMATCHINFO_MODELINFO
                , create_if_notexist: true, reset: true);
            wrModel(WrMod.ModelINFO);
            Read();
            getSavedRules();
            Handler();
            wrModel(WrMod.Report);
            if (!isReportConsistent()) Msg.F("internal error");
        }

        private bool isReportConsistent()
        {
#endif  //OLD
        private void Pricing()
        {
            if (elements.Count == 0) Msg.F("elements.Count == 0");
            Docs dRep = Docs.getDoc(Decl.TSMATCHINFO_REPORT);
            dRep.Reset();
            //12/5            mj = new ModelJournal.ModJournal(boot.models);
            //24/5            Mod m = mj.getModJournal(name);
            //24/5            strListRules = m.strListRules;
            getSavedRules();
            //12/5            mh = new ModelHandler.ModHandler();
            mh.Handler(this);
//7/6            getSavedRules();

            return;
            throw new NotImplementedException();
            //8/5            getGroups();
            //12/5 getSavedGroups();
            //6/5           getSavedRules();
            //6/5           if (R
            //12/5 ules.Count <= 0) return false;
            //7/5            if (docReport.Body.Double(docReport.il, Decl.REPORT_SUPL_PRICE) <= 0.0) return false;
            //12/5                mj = new ModelJournal.ModJournal(boot.models);
            //14/5                Mod m = mj.getModJournal(name);
            //16/5---------------- перенести это в Pricing() ---------------
            // пока почему-то нужно вызывать Handling -- делается Reset(Report)
            //16/5                getSavedRules();
            //16/5                mh.Handler(this);
            // если здесь isChanged=true -- mj.SaveModJournal
            //13/5              strListRules = m.strListRules;
            //16/5                elmGroups = mh.elmGroups;
            //16/5                Log.Trace("*SR.elements=", elements.Count, " gr=", elmGroups.Count);
            //16/5-----------------------------------------------------------
        }

        private void ChangedPricing()
        {
            throw new NotImplementedException();
        }

        public void ChangedModel()
        {
            Msg.AskFOK("Нет сохраненной корректной модели. Читаем модель заново?");
            Reset(sINFO);
            elements = Raw(this, write: true);
            Reset(sRep);
        }

        private void Reset(string doc_name)
        {
            if (!Docs.IsDocExists(doc_name)) Recover(doc_name, RecoverToDo.CreateRep);
            Msg.AskFOK("Вы действительно намерены переписать TSmatchINFO.xlsx/Report?");
            Recover(doc_name, RecoverToDo.ResetRep);
        }

        public enum RecoverToDo
        {
            CreateRep, ResetRep, NewMod,
            ChangedDir,
            ChangedPricing
        }
        public void Recover(string repNm, RecoverToDo to_do)
        {
            switch (to_do)
            {
                case RecoverToDo.CreateRep:
                    Docs.getDoc(repNm, reset: true, create_if_notexist: true);
                    if (!Docs.IsDocExists(repNm)) Msg.F("SaveDoc.Recover cannot create ", repNm);
                    Recover(repNm, RecoverToDo.ResetRep);
                    break;
                case RecoverToDo.ResetRep:
                    switch (repNm)
                    {
                        case Decl.TSMATCHINFO_MODELINFO:
                            wrModel(WrMod.ModelINFO);
                            break;
                        case Decl.TSMATCHINFO_REPORT:
                            wrModel(WrMod.Report);
                            break;
                    }
                    break;
            }
        }

        /// <summary>
        /// SetFrSavedModelINFO(string dir) - set model attributes from 
        /// TSmatchINFO.xlsx/ModuleINFO. When this documents corrupred - Recover it
        /// </summary>
        /// <param name="dir">directory, where TSmatchINFO.xlsx stored</param>
        public Mod SetFrSavedModelINFO(string dir)
        {
            dINFO = Docs.getDoc(sINFO, fatal: false);
            if (dINFO == null || dINFO.il < 9
                || isChangedStr(ref name, dINFO, 2, 2))
            {
                Reset(sINFO);
                SetFrSavedModelINFO(dir);
            }
            name = dINFO.Body.Strng(2, 2);
            phase = dINFO.Body.Strng(4, 2);
            date = DateTime.Parse(dINFO.Body.Strng(5, 2));
            MD5 = dINFO.Body.Strng(6, 2);
            if (elementsCount == 0 && !TS.isTeklaActive())
                elementsCount = dINFO.Body.Int(7, 2);
            pricingDate = DateTime.Parse(dINFO.Body.Strng(8, 2));
            pricingMD5 = dINFO.Body.Strng(9, 2);
            //20/5            Mod m = mj.SetFromModJournal(name, dir);
            //20/5            strListRules = m.strListRules;
            return this;
#if ToReview    //8/5
            //7/5            iModJounal = getModJournal(name);
            //7/5            string dateJrn = getModJrnValue(Decl.MODEL_DATE);
            //7/5            if (isChangedStr(ref dateJrn, dINFO, 5, 2)) goto Rec;
            //7/5            date = DateTime.Parse(dateJrn);
            //8/5            if (date > DateTime.Now || date < old) goto Err;
            Err:
                Msg.F("SavedReport doc not exists", dir);
            Rec:
                Recover(dINFO.name, RecoverToDo.ChangedMod);
            SetFrSavedModelINFO(dir);
#endif
        }

        /// <summary>
        /// Raw() - read elements from Raw.xml or re-write it, if necessary 
        ///<para>
        ///re-write reasons: Raw.xml not exists, MD5 or elementsCount != ones in ModelINFO
        ///</para>
        /// </summary>
        /// <returns>updated list of elements in file and in memory</returns>
        public List<Elm> Raw(Mod mod, bool write = false)
        {
            Log.set("SR.Raw(" + mod.name + ")");
            List<Elm> elms = new List<Elm>();
            string file = Path.Combine(mod.dir, Decl.RAWXML);
            if (!write && FileOp.isFileExist(file))
            {                               // Read Raw.xml
                elms = rwXML.XML.ReadFromXmlFile<List<Elm>>(file);
            }
            else
            {                               // get from CAD and Write or re-Write Raw.xml 
                Msg.AskFOK("Файл Raw.xml не доступен."
                    + " Вы действительно хотите получить его из САПР заново?");
                mod.Read();
                rwXML.XML.WriteToXmlFile(file, mod.elements);
                elms = mod.elements;
            }
            if (mod.elementsCount != elms.Count) elms = Raw(mod, write: true);
            Docs docModelINFO = Docs.getDoc(Decl.TSMATCHINFO_MODELINFO);
            if (mod.MD5 != docModelINFO.Body.Strng(6, 2)
                || elementsCount != docModelINFO.Body.Int(7, 2)) Reset(sINFO);
            Log.Trace("{ elmCount, MD5} ==", elms.Count, mod.MD5);
            Log.exit();
            return elms;
        }

        private bool isChangedStr(ref string str, Docs doc, int row, int col)
        {
            string strINFO = doc.Body.Strng(row, col);
            if (string.IsNullOrEmpty(str)) str = strINFO;
            return str != strINFO;
        }
        private bool isChangedInt(ref int n, Docs doc, int row, int col)
        {
            int nINFO = doc.Body.Int(row, col);
            if (n == 0) n = nINFO;
            return n != nINFO;
        }

        public void getSavedGroups()
        {
            if (elmGroups.Count == 0) Msg.F("SavedReport.getSavedGroup: elmGroups.Count = 0");
            string sRep = Decl.TSMATCHINFO_REPORT;
            Docs dRep = Docs.getDoc(sRep, fatal: false);
            if (dRep == null || dRep.il != (elmGroups.Count + dRep.i0)) Reset(sRep);
            double totalPrice = dRep.Body.Double(dRep.il, Decl.REPORT_SUPL_PRICE);
            //17/5            if (totalPrice == 0) Pricing();
            int gr_n = dRep.i0;
            foreach (var gr in elmGroups)
            {
                string grPrice = dRep.Body.Strng(gr_n, Decl.REPORT_SUPL_PRICE);
                gr.totalPrice = Lib.ToDouble(grPrice);
                gr.SupplierName = dRep.Body.Strng(gr_n, Decl.REPORT_SUPPLIER);
                gr.CompSetName = dRep.Body.Strng(gr_n, Decl.REPORT_COMPSET);
                gr_n++;
            }
            pricingMD5 = get_pricingMD5(elmGroups);
        }
        public void getSavedRules()
        {
            Log.set("SR.getSavedRules()");
            Rules.Clear();
            Docs doc = Docs.getDoc("Rules");
            for (int i = doc.i0; i <= doc.il; i++)
            {
                try   { Rules.Add(new Rule.Rule(i)); }
                catch { continue; }
                //////////////////date = Lib.getDateTime(doc.Body.Strng(i, 1));
                //////////////////if (date > DateTime.Now || date < Decl.OLD) continue;
                //////////////////string sSupl = doc.Body.Strng(i, 2);
                //////////////////string sCS = doc.Body.Strng(i, 3);
                // 7/6/17 ////////string sR = doc.Body.Strng(i, 4);
                //////////////////if (string.IsNullOrEmpty(sSupl)
                //////////////////    || string.IsNullOrEmpty(sCS)
                //////////////////    || string.IsNullOrEmpty(sR)) continue;
                //////////////////var rule = new Rule.Rule(date, sSupl, sCS, sR);
                //////////////////Rules.Add(rule);
            }
            log.Info("- getSavedRules() Rules.Count = " + Rules.Count);
            Log.exit();
        }

        internal void Save(Mod model, bool isRuleChanged)
        {
            // переложим все необходимый атрибуты для ModelINFO из model в this
            name = model.name;
            dir = model.dir;
            phase = model.phase;
            date = model.date;
            made = model.made;
            MD5 = model.MD5;
            elementsCount = model.elementsCount;
            pricingDate = model.pricingDate;
            pricingMD5 = model.pricingMD5;

            elements = model.elements;
            elmGroups = model.elmGroups;
            Rules = model.Rules;

            // теперь запишем в файл
            wrModel(WrMod.ModelINFO);
            wrModel(WrMod.Report);
            if (isRuleChanged) wrModel(WrMod.Rules);
        }

        public void CloseReport()
        {
            dINFO.Close();
        }
    } // end class SavedReport
} // end namespace