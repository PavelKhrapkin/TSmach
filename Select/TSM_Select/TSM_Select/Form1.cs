﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using TS = TSmatch.Tekla.Tekla;
using Mod = TSmatch.Model.Model;
using Boot = TSmatch.Bootstrap.Bootstrap;

namespace TSM_Select
{
    public partial class Form1 : Form
    {
        Boot boot;
        Mod model;
        TS ts = new TS();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            boot = new Boot("init");
            model = boot.model;
            if (model.getSavedReport())
            {
                // тут надо спросить пользователя "Будем читать из САПР?"
                //..пока читаю всегда..
                model.getSavedReport(true);
            }
            WrForm(wrForm.modelINFO);
            WrForm(wrForm.modelReport);   
        }

        enum wrForm { modelINFO, modelReport };
        private void WrForm(wrForm wrf)
        {
            switch (wrf)
            {
                case wrForm.modelINFO:
                    groupBox_modINFO.Text = "Общая информация о модели";
                    modINFO.Text = 
                            "Модель \"" + model.name + "\""
                        + "\nДата сохранения " + model.date.ToLongDateString()
                                         + " " + model.date.ToShortTimeString()
                        + "\nВсего " + model.elementsCount + " элементов"
                             + " в " + model.elmGroups.Count + " группах";
                    modINFO.BackColor = Color.LightBlue;
                    break;

                case wrForm.modelReport:
   //                 ListBox rep = new ListBox();
                    //8/4                    rep.Size = new Size(300, 300);
                    List<string> modRep = new List<string>();
                    foreach(var gr in model.elmGroups)
                    {
                        string str = gr.mat + "\t" + gr.prf + "\t" + gr.totalPrice;
                        modRep.Add(str);
                    }
                    listBox1.DataSource = modRep;
                    break;
            }
//            throw new NotImplementedException();
        }

        private void OK_Click(object sender, EventArgs e)
        {      
            ts.HighlightClear();
            model.docReport.Close();
            boot.docTSmatch.Close();
            Application.Exit();
        }

        private void elementsCount_Click(object sender, EventArgs e)
        {
            modINFO.Text = "Читаю модель заново";
            modINFO.BackColor = Color.Yellow;
            ts.Read();
            WrForm(wrForm.modelINFO);
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
