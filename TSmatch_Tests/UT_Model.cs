﻿/*=================================
* Model Unit Test 21.08.2017
*=================================
*/
using TSmatch.Model;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using FileOp = match.FileOp.FileOp;
using Docs = TSmatch.Document.Document;
using Boot = TSmatch.Bootstrap.Bootstrap;
using Elm = TSmatch.ElmAttSet.ElmAttSet;
using Mod = TSmatch.Model.Model;
using Decl = TSmatch.Declaration.Declaration;
using SR = TSmatch.SaveReport.SavedReport;
using MH = TSmatch.Handler.Handler;

namespace TSmatch.Model.Tests
{
    [TestClass()]
    public class UT_Model
    {
        Boot boot;
        Mod model;

        [TestMethod()]
        public void UT_setCity()
        {
            string str = "Cанкт-Петербург, Кудрово";

            Mod mod = new Mod();
            mod.setCity(str);

            Assert.AreEqual("Cанкт-Петербург", mod.adrCity);
            Assert.AreEqual("Кудрово", mod.adrStreet);
        }

        [TestMethod()]
        public void UT_getMD5()
        {
            var model = new Mod();
            Assert.AreEqual(0, model.elements.Count);

            // test empty list of elements MD5
            string md5 = model.getMD5(model.elements);
            Assert.AreEqual("4F76940A4522CE97A52FFEE1FBE74DA2", md5);

            // test getMD5 with Raw()
            boot = new Boot();
            var sr = new SR();
            model = sr.SetModel(boot, initSupl: true);
            model.elements = sr.Raw(model);
            Assert.IsTrue(model.elements.Count > 0);
            string MD5 = model.getMD5(model.elements);
            Assert.AreEqual(32, MD5.Length);
            Assert.IsTrue(MD5 != md5);

            // test -- проверка повторного вычисления MD5
            string MD5_1 = model.getMD5(model.elements);
            Assert.AreEqual(MD5_1, MD5);

            FileOp.AppQuit();
        }


        [TestMethod()]
        public void UT_get_pricingMD5()
        {
            var model = new Mod();
            Assert.AreEqual(0, model.elements.Count);
            Assert.AreEqual(0, model.elmGroups.Count);

            // test empty list of groups pricingMD5
            string pricingMD5 = model.get_pricingMD5(model.elmGroups);
            const string EMPTY_GROUP_LIST_PRICINGMD5 = "5E7AD112B9369E41723DDFD797758E62";
            Assert.AreEqual(EMPTY_GROUP_LIST_PRICINGMD5, pricingMD5);

            // test real model and TSmatchINFO.xlsx
            var boot = new Boot();
            var sr = new SR();
            model = sr.SetModel(boot, initSupl: true);
            model.sr = new SR();
            model.elements = model.sr.Raw(model);
            var mh = new MH();
            var grp = mh.getGrps(model.elements);

            pricingMD5 = model.get_pricingMD5(grp);

            Assert.IsNotNull(pricingMD5);
            Assert.AreEqual(32, pricingMD5.Length);
            Assert.IsTrue(EMPTY_GROUP_LIST_PRICINGMD5 != pricingMD5);

            FileOp.AppQuit();
        }
    }
}