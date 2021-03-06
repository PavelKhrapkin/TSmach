﻿/*=================================
 * Model.Handler Unit Test 19.6.2017
 *=================================
 */
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TSmatch.Model.Handler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FileOp = match.FileOp.FileOp;
using Boot = TSmatch.Bootstrap.Bootstrap;
using Mod = TSmatch.Model.Model;
using Msg = TSmatch.Message.Message;

namespace TSmatch.Model.Handler.Tests
{
    [TestClass()]
    public class UT_ModHandler
    {
        [TestMethod()]
        public void UT_ModHandler_PrfUpdate()
        {
            var mod = new ModHandler();
            ElmAttSet.Group gr = new ElmAttSet.Group();

            // test 1: "—100*6" => "—6x100"
            gr.Prf = gr.prf = "—100*6";
            mod.elmGroups.Add(gr);
            mod.PrfUpdate();
            var v = mod.elmGroups[0].prf;
            Assert.AreEqual(v, "—6x100");

            // test 2: "—100*6" => "—6x100"
            gr.Prf = gr.prf = "—100*6";
            mod.elmGroups.Add(gr);
            mod.PrfUpdate();
            v = mod.elmGroups[1].prf;
            Assert.AreEqual(v, "—6x100");

            // test 3: "L75X5_8509_93" => L75x5"
            gr.Prf = gr.prf = "L75X5_8509_93";
            mod.elmGroups.Add(gr);
            mod.PrfUpdate();
            v = mod.elmGroups[2].prf;
            Assert.AreEqual(v, "L75x5");
        }

        [TestMethod()]
        public void UT_ModHandler_geGroup_Native()
        {
            var boot = new Boot();
            var model = new Mod();
            model.SetModel(boot);
        }

        [TestMethod()]
        public void UT_Hndl()
        {
            var boot = new Boot();
            var model = new Mod();
            model.SetModel(boot);

            var mh = new ModHandler();
            mh.Hndl(model);
            int cnt = 0;
            foreach (var gr in model.elmGroups) cnt += gr.guids.Count();
            Assert.AreEqual(model.elements.Count(), cnt);

            //Hndl performance test -- 180 sec for 100 cycles
            DateTime t0 = DateTime.Now;
            for (int i = 0; i < 100; i++)
            {
                mh.Hndl(model);
            }
            TimeSpan ts = DateTime.Now - t0;
            Assert.IsTrue(ts.TotalSeconds > 0.0);
        }

        [TestMethod()]
        public void UT_Pricing()
        {
            var boot = new Boot();
            var model = new Mod();
            model.SetModel(boot);

            var mh = new ModHandler();
            mh.Pricing(ref model);
            Assert.IsTrue(model.matches.Count > 0);
        }
    }
}