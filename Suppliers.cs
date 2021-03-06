﻿/*----------------------------------------------------------------------------
 * Suppliers - componets supplier organisations
 * 
 *  3.10.2017  Pavel Khrapkin
 *
 *--- History ---
 * 27.4.2016 - Remove List<string> doc_names from the Supplier class
 * 29.11.2016 - get Supplier directly from TSmach.xlsx/Supplier, not from Suppliers List
 * 16.04.2017 - getSupplierStr() method add for Windows Form use
 * 10.05.2017 - Not existing Supplier Handle
 * 26.07.2017 - move getSupplierStr to MainWindow
 *  3.10.2017 - adapted to Unit Test, refactoring
 * ---------------------------------------------------------------------------
 *      METHODS:
 * getSupplier(name)    - create Suplier(name), get data from TSmatch.xlsx/Supplier    
 * getSupplierStr()		- return Supplier data in string to be used with Form 
 * 
 * -------------- My Report and Debugging -----------
 * SupplReport()    - Suppliers full list output
 */
using System;
using System.Collections.Generic;
using CmpSet = TSmatch.CompSet.CompSet;
using Decl = TSmatch.Declaration.Declaration;
using Docs = TSmatch.Document.Document;
using Gr = TSmatch.Group.Group;
using Lib = match.Lib.MatchLib;

namespace TSmatch.Suppliers
{
    /// <summary>
    /// Suppliers - class of Component' Suppliers. The name of Suppler should be Unique
    /// </summary>
    public class Supplier : IComparable<Supplier>
    {
        Message.Message Msg = new Message.Message();

        public DateTime Date;   // Last Update Supplier' Date
        public string Name;
        public string Url;
        public string City;
        public string Index;
        public string Street;
        public string Country;
        public string Telephone;
        public string Contact;
        public List<CmpSet> CompSets = new List<CmpSet>();

        /// <summary>
        /// Supplier Constructor
        /// </summary>
        /// <param name="date">last updated</param>
        /// <param name="name">Supplier name</param>
        /// <param name="url">hyperlink - Web page of the Suppliers </param>
        /// <param name="city">city to delivery supplies from</param>
        /// <param name="street">street address</param>
        /// <param name="index">post intex of the Supplier</param>
        /// <param name="tel">Telephone of the Supplier</param>
        /// <param name="List<CompSet> CompSets">collection of CompSet related to the Supplier</param>
        public Supplier(DateTime date, string name, string url, string city, string street, string index, string tel, List<CmpSet> cs)
        {
            Date = date;
            Name = name;
            Url = url;
            City = city;
            Index = index;
            Street = street;
            Telephone = tel;
            CompSets = cs;
        }
        public Supplier(int n) { getSupplier(n); }

        public Supplier(string _name, bool init=true)
        {
            Name = _name;
            if (!init) return;
            Docs docSupl = Docs.getDoc(Decl.SUPPLIERS);
            for (int i = docSupl.i0; i <= docSupl.il; i++)
            {
                string suplName = docSupl.Body.Strng(i, Decl.SUPL_NAME);
                if (suplName != _name) continue;
                getSupplier(i);
                return;
            }
            Msg.W("No such Supplier(" + _name + ")");
        }

        private void getSupplier(int n)
        {
            Docs docSupl = Docs.getDoc(Decl.SUPPLIERS);
            Date = Lib.getDateTime(docSupl.Body[n, Decl.SUPL_DATE]);
            Name = (string)docSupl.Body[n, Decl.SUPL_NAME];
            Url = (string)docSupl.Body[n, Decl.SUPL_URL];
            City = (string)docSupl.Body[n, Decl.SUPL_CITY];
            Street = (string)docSupl.Body[n, Decl.SUPL_STREET];
            Index = (string)docSupl.Body[n, Decl.SUPL_INDEX];
            Telephone = (string)docSupl.Body[n, Decl.SUPL_TEL];
        }

        /// <summary>
        /// CompareTo(Supplier) implements comparision of "this" with the supplier as a parametr. 
        ///     It is used to Sort Suppliers by City and level of readiness to handle in TSmatch
        /// </summary>
        /// <param name="supl"></param>
        /// <returns></returns>
        public int CompareTo(Supplier supl)
        {
            int result = this.City.CompareTo(supl.City);
            if (result == 0)
            {
                result = -this.CompSets.Count.CompareTo(supl.CompSets.Count);
            }
            return result;
        }
        /// <summary>
        /// getSupplier(string name) - get supplier data from the list of supplers in TSmatch.xlsx/Suppliuers by the name
        /// </summary>
        /// <param name="name">name of the supplier to find</param>
        /// <returns>found supplier of null</returns>
        /// <history>27.3.2016
        /// 29.11.2016 - re-written. return data directly from TSmatch/xlsx/Suppliers
        /// </history>
        internal static Supplier getSupplier(string name)
        {
            return new Supplier(name);
        }
        /// <summary>
        /// SupplReport() - Debugging report: output list of Supplier companies in TSmatch.xlsx/Suppliers
        /// </summary>
        public static void SupplReport()
        {
            List<Supplier> Suppliers = new List<Supplier>();
            Docs docSupl = Docs.getDoc(Decl.SUPPLIERS);
            for (int i = docSupl.i0; i <= docSupl.il; i++)
            {
                Suppliers.Add(new Supplier(i));
            }
            Docs doc = Docs.getDoc("SupplReport");
            doc.Reset("Now");
            foreach (var s in Suppliers)
            {
                doc.wrDoc(1, s.Date, s.Name, s.Url, s.City, s.Index, s.Street, s.Telephone, s.CompSets.Count);
                foreach (var cs in s.CompSets)
                {
                    //11.1.17                    cs.getCompSet();
                    //////CmpSet.getCompSet(cs.name, s);
                    //////Docs w = Docs.getDoc(cs.doc.name);
                    //////cs.doc = w;
                    Docs w = cs.doc;
                    string nm = w.Wb.Name + "/" + w.Sheet.Name;
                    doc.wrDoc(2, w.name, nm, w.i0, w.il, w.LoadDescription);
                }
                foreach (var cs in s.CompSets) cs.doc.Close();
            }
            doc.saveDoc();
            doc.Close();
        }

        /// <summary>
        /// getNEWcs(Supplier, Group) - return CompSet could be supplied by supl, or null if not available
        /// </summary>
        /// <param name="selSupl"></param>
        /// <param name="grOLD"></param>
        /// <returns></returns>
        internal CmpSet getNEWcs(Supplier supl, Gr group)
        {
            string grCSname = group.CompSetName;
            CmpSet cs;
            try { cs = new CmpSet(grCSname, this); }
            catch { return null; }

            //////////if (CompSets.Count == 0)
            //////////{

            //////////}
            //////////var cs = CompSets.Find(x => x.name == grCSname);
            //////////if (cs != null) return cs; // нашел CS с тем же именем!
            // в дальнейшем тут вставить подбор CompSet по-компонентно с учетом Rule.synonyms
            // это означает, что, хотя у CS другое название, его разрешено использовать Правилами.
            var mod = MainWindow.model;
            if (mod.Rules.Count == 0) return null; // тут надо загрузить Правила из TSmatchINFO.xlsx/Rules
            Rule.Rule rule = null;
            if (group.match != null) rule = group.match.rule;
            cs = CompSets.Find(x => x.name == rule.CompSet.name);
            if (cs != null) return cs;    // true - found CompSet.name from this Suppler
            //.. по крайней мере, имя отличается -- проверим по-компонентно все прайс-листы с Match
            //.. еще не написано
            return cs;
        }

        /// <summary>
        /// CheckCS(group) return true, when this Supplier
        /// containes same material and profule, or rule allow replacement
        /// </summary>
        /// <param name="group"></param>
        /// <param name="rule"></param>
        /// <returns></returns>
        internal bool CheckCS(Gr group)
        {
            string grCSname = group.CompSetName;
            var cs = CompSets.Find(x => x.name == grCSname);
            if (cs != null) return true; // нашел CS с тем же именем!

            return false;
        }
    } // end class Supplier
} // end namespace Suppliers