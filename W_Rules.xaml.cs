﻿/*-----------------------------------------------
 * WPF Window W_Rules 25.5.2017 Pavel Khrapkin
 * ----------------------------------------------
 * --- History ---
 * 2017.05.25 - written
 * --- Known Issue & ToDos ---
 * - еще нет диалога по допустимости CompSet для выбранного поставщика
 * - не написан метод ChekIfChanges()
 */
using System;
using System.Collections.Generic;
using System.Windows;
using log4net;

using Lib = match.Lib.MatchLib;
using Decl = TSmatch.Declaration.Declaration;
using Docs = TSmatch.Document.Document;
using System.Linq;

namespace TSmatch
{
    /// <summary>
    /// Interaction logic for W_Rules.xaml
    /// </summary>
    public partial class W_Rules : Window
    {
        public static readonly ILog log = LogManager.GetLogger("W_Rules");

        List<Rule.Rule> rules = new List<Rule.Rule>();

        public W_Rules()
        {
            InitializeComponent();
            Title = "TSmatch: работа с правилами";
            List<Rl> items = new List<Rl>();
            Docs doc = Docs.getDoc(Decl.TSMATCHINFO_RULES);

            for (int i = doc.i0; i <= doc.il; i++)
            {
                string csName = doc.Body.Strng(i, Decl.RULE_COMPSETNAME);
                Rule.Rule rule = new Rule.Rule(i);
                if (rule == null || string.IsNullOrWhiteSpace(rule.sSupl)
                    || string.IsNullOrWhiteSpace(rule.text)) continue;
                int nElms = 0;
                foreach(var mtch in MainWindow.model.matches)
                {
                    if (mtch.rule.sSupl != rule.sSupl || mtch.rule.sCS != rule.sCS) continue;
                    var gr = mtch.group;
                    nElms += gr.guids.Count;
                }
                items.Add(new Rl(nElms, rule.date, rule.sSupl, csName, rule.text));
                rules.Add(rule);
            }
            WRules.ItemsSource = items;
        }

        //private void OnRule_changed(object sender, SelectionChangedEventHandled y) //, SelectionChangedEventArgs e)
        //{
        //}

        private void OK_button_Click(object sender, RoutedEventArgs e)
        {
            CheckIfChanges();
            Close();
        }

        private void CheckIfChanges()
        {
            //25/5            throw new NotImplementedException();
        }

        private class Rl : IComparable<Rl>
        {
            public string nElms { get; set; }
            public string Date { get; set; }
            public string Supplier { get; set; }
            public string CompSet { get; set; }
            public string RuleText { get; set; }
            public bool Flag { get; set; }

            public Rl(int _nElms, DateTime date, string supl, string cs, string ruletxt)
            {
                nElms = _nElms.ToString();
                Date = date.ToString("d.MM.yy H:mm");
                Supplier = supl;
                CompSet = cs;
                RuleText = ruletxt;
            }

            int IComparable<Rl>.CompareTo(Rl other)
            {
                if (Flag && !other.Flag) return -1;
                if (!Flag && other.Flag) return 1;
                int result = -CompSet.CompareTo(other.CompSet);
                if (result == 0) result = Supplier.CompareTo(other.Supplier);
                return result;
            }
        }
    }
} // end namespace
