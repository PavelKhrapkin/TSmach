/*--------------------------------------------
 * FileOp - File System primitives
 * 
 *  12.03.2016  Pavel Khrapkin, Alex Pass
 *
 *--- JOURNAL ---
 * 2013 - 2013 - created
 * 12.3.2016 - isNamedRangeExist(name)  
 * -------------------------------------------        
 * fileOpen(dir,name[,create])  - Open or Create file name in dir catalog
 * isFileExist(name)            - return true if file name exists
 * isSheetExist(Wb, name)       - return true if Worksheet name exists in Workbook Wb
 * isNamedRangeExist(Wb, name)  - return true when named range name exists in Wb 
 * getRngValue(Sheet,r0c0, r1c1, msg)   - return Mtr-Range content from Sheet in Range [r0c0r1c1]
 * getSheetValue(Sheet,msg)             - return Mtr-Range from UsedRange in Sheet
 * saveRngValue(Body [,row_to_ins) - write Document Body content to Excel file 
 * setRange(..)                 - few overloaded methods to set Renge -- preparation for saveRngValue(..)
 * CopyRng(Wb,NamedRange,rng)   - copy named range NamedRange into rng in Workbook Wb
?* FormCol(char col[, dig])     - format column col in Excel file as a Number with dig decimal digits
 */
using System;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;
using match.Lib;
using Mtr = match.Matrix.Matr;
using Msg = TSmatch.Message.Message;

namespace match.FileOp
{
    public class FileOp
    {
        public static string dirDBs = null;
        private static Excel.Application _app = null;
        private static Excel.Workbook _wb = null;
        private static Excel.Worksheet _sh = null;
        private static Excel.Range _rng = null;

        /// <summary>
        /// fileOpen(dir,name[,create_ifnotexist]) - ��������� ���� Excel �� ����� name
        /// </summary>
        /// <param name="dir">������� ������������ �����</param>
        /// <param name="name">��� ������������ �����</param>
        /// <param name="create_ifnotexist">optional flag - ���������, ���� ����� ���� �� ����������</param>
        /// <returns>Excel.Workbook</returns>
        /// <journal>11.12.2013
        ///  7.01.14 - ������ ����� ������ �� ������ � finally
        /// 22.12.14 - ��������� � ������� ���������� �����
        /// 24.01.15 - setDirDBs �������� � ��������� ������������
        ///  1.02.15 - �������� ����� Quit()
        /// 17.01.16 - �������������� � ��������� ���� FileOp.cs, ��������� [,create_ifnotexist]
        /// 27.01.16 - �������� "create_ifnotexist" ����
        /// </journal>
        public static Excel.Workbook fileOpen(string dir, string name, bool create_ifnotexist = false)
        {
            Log.set("fileOpen");
            if (_app == null) _app = new Excel.Application();   // Excel �� ������� -> ���������
            Excel.Workbook Wb = null;
            bool found = false;
            foreach (Excel.Workbook W in _app.Workbooks)
                if (W.Name == name) { Wb = W; found = true; break; }
            if (!found)
            {
                string file = dir + "\\" + name;
                try
                {       // -- ������� ������ ��� ������� ���� --
                    if (create_ifnotexist)
                    {
                        if (isFileExist(file)) Wb = _app.Workbooks.Open(file);
                        else
                        {
                            Wb = _app.Workbooks.Add();
                            Wb.SaveAs(file);
                        }
                    }
                    else Wb = _app.Workbooks.Open(file);
                    _app.Visible = true;
                }
                catch (Exception ex) { Log.FATAL("�� ������ ���� " + file + "\n ��������� �� CATCH= '" + ex); }
            }
            Log.exit();
            return Wb;
        }
        public static void DisplayAlert(bool val) { _app.DisplayAlerts = val; }
        public static void fileSave(Excel.Workbook Wb) { Wb.Save(); }
        public static bool isFileExist(string name)
        {
            Log.set("isFileExist(" + name + ") ?");
            bool result = false;
            try
            {
                result = File.Exists(name);
            }
            catch { result = false; }
            finally { Log.exit(); }
            return result;
        }
        public static bool isFileExist(string dir, string name)
        {
            return isFileExist(dir + "\\" + name);
        }
        public static bool isSheetExist(Excel.Workbook Wb, string name)
        {
            try { Excel.Worksheet Sh = Wb.Worksheets[name]; return true; }
            catch { return false; }
        }
        public static bool isNamedRangeExist(Excel.Workbook Wb, string name)
        {
            bool result = true;
            try
            {
                result = Wb.Names.Item(name) != null;
            }
            catch ( Exception e)
            {
                if (Msg.Trace) Msg.I("TRACE_33.1_IsNameRangeExist", e, name);
                result = false;
            }
            return result;
        }
        public static Excel.Worksheet SheetReset(Excel.Workbook Wb, string name, bool QuietMode = false)
        {
            Log.set(@"SheetReset(" + Wb.Name + "/" + name + ")");
            try
            {
                if (isSheetExist(Wb, name))
                {
                    Excel.Worksheet oldSh = Wb.Worksheets[name];
                    Wb.Worksheets.Add(Before: oldSh);
                    _sh = Wb.ActiveSheet;
                    Wb.Application.DisplayAlerts = false;
                        oldSh.Delete();
                    Wb.Application.DisplayAlerts = true;
                }
                else
                {
                    if(!QuietMode)
                        Log.Warning("����(" + Wb.Name + "/" + name + ") �� �����������. ������ �����.");
                    Wb.Worksheets.Add();
                    _sh = Wb.ActiveSheet;
                }
                _sh.Name = name;
            } catch (Exception e) { Log.FATAL("������ \"" + e.Message + "\""); }
            Log.exit();
            return _sh;
        }
        public static Mtr getRngValue(Excel.Worksheet Sh, int r0, int c0, int r1, int c1, string msg = "")
        {
            Log.set("getRngValue");
            try
            {
                Excel.Range cell1 = Sh.Cells[r0, c0];
                Excel.Range cell2 = Sh.Cells[r1, c1];
                Excel.Range rng = Sh.Range[cell1, cell2];
                return new Mtr(rng.get_Value());
            }
            catch
            {
                if (msg == "")
                {
                    msg = "Range[ [" + r0 + ", " + c0 + "] , [" + r1 + ", " + c1 + "] ]";
                }
                Log.FATAL(msg);
                return null;
            }
            finally { Log.exit(); }
        }
        public static Mtr getSheetValue(Excel.Worksheet Sh, string msg = "")
        {
            Log.set("getSheetValue");
            try { return new Mtr(Sh.UsedRange.get_Value()); }
            catch
            {
                if (msg == "") msg = "���� \"" + Sh.Name + "\"";
                Log.FATAL(msg);
                return null;
            }
            finally { Log.exit(); }
        }
        public static void saveRngValue(Mtr Body, int rowToPaste = 1, bool AutoFit = true, string msg = "")
        {
            Log.set("saveRngValue");
            int r0 = Body.LBoundR(), r1 = Body.iEOL(),    //!!
                c0 = Body.LBoundC(), c1 = Body.iEOC();    //!!
            try
            {
                object[,] obj = new object[r1, c1];
                for (int i = 0; i < r1; i++)
                    for (int j = 0; j < c1; j++)
                        obj[i, j] = Body[i + 1, j + 1];
                r1 = r1 - r0 + rowToPaste;
                r0 = rowToPaste;
                Excel.Range cell1 = _sh.Cells[r0, c0];
                Excel.Range cell2 = _sh.Cells[r1, c1];
                Excel.Range rng = _sh.Range[cell1, cell2];
                rng.Value = obj;
                if( AutoFit) for (int i = 1; i <= c1; i++) _sh.Columns[i].AutoFit();
            }
            catch (Exception e)
            {
                if (msg == "")
                { msg = "Range[ [" + r0 + ", " + c0 + "] , [" + r1 + ", " + c1 + "] ]"; }
                Log.FATAL(msg);
            }
            Log.exit();
        }
        /// <summary>
        /// setRange ������������� ��������, � ����� Sh, ������� ����� ���������� ������ FileOp
        /// </summary>
        /// <param name="Sh">����, � ������� ������������� ��������</param>
        /// <param name="r0"></param>
        /// <param name="c0"></param>
        /// <param name="r1"></param>
        /// <param name="c1"></param>
        public static Excel.Range setRange(Excel.Worksheet Sh, int r0 = 1, int c0 = 1, int r1 = 0, int c1 = 0)
        {
            try
            {
                if (r1 == 0) r1 = r0;
                if (c1 == 0) c1 = c0;
                _sh = Sh;
                Excel.Range cell1 = _sh.Cells[r0, c0];
                Excel.Range cell2 = _sh.Cells[r1, c1];
                _rng = _sh.Range[cell1, cell2];
                return _rng;
            }
            catch (Exception e)
            {
                Log.FATAL("Internal Error: " + e.Message
                            + "\nSheet(" + _sh.Name + ") Excel.Range"
                            + "[ [" + r0 + ", " + c0 + "], [" + r1 + ", " + c1 + "] ]");
                return null;
            }
        }
        public Excel.Worksheet setSheet()
        { _sh = (Excel.Worksheet)this; return _sh; }
        public Excel.Range setRange() { return _rng; }
        public static Excel.Range setRange(string NamedRange)
        { return _wb.Names.Item(NamedRange).RefersToRange.Select(); }
        public static void setRange(Excel.Workbook Wb, string NamedRange)
        { Wb.Names.Item(NamedRange).RefersToRange.Select(); }
        public static void CopyRng(Excel.Workbook Wb, string NamedRange, Excel.Range rng)
        {
            Wb.Names.Item(NamedRange).RefersToRange.Copy(rng);
        }
        public static void FormCol(char col, int dig = 0)
        {
        }
    }  // end class FileOp
}  // end namespace FileOp
#if NOT_IN_USE
 /*
 * ........ �� ������������ ............
 * WrCSV(name)          - ���������� CSV ���� ��� ��� ����������� ���� � SalesForce
 * WrReport(name,dt)    - ���������� ��������� ���� name � ������� �������
 * Quit()               - ��������� Excel - � �������� ��� UnitTest
 * -------- private ������ ----------------
 * setDirDBs()          - ��������� Windows Environment � ������������� ������� dirDBs
 */   
    /// <summary>
    /// WrReport(name,dt)   - ���������� ��������� ���� name � ������� �������
    /// </summary>
    /// <param name="name">string name - ��� ����� - ������ *.txt</param>
    /// <param name="dt">DataTable dt - ������� � ������� ��� ������</param>
    /// <journal>23.01.2015</journal>
    public static void WrReport(string name, DataTable dt)
    {
        setDirDBs();
        string fileName = dirDBs + @"\Reports\" + name + @".txt";
        using (StreamWriter fs = new StreamWriter( fileName, true, System.Text.Encoding.Default))
        {
            fs.WriteLine("--- " + DateTime.Now.ToLongTimeString() + " " + name + " ------------------");
            foreach (DataRow row in dt.Rows)
            {
                string str = "";
                foreach (DataColumn x in dt.Columns)
                {
                    if (str != "") str += '\t';
                    str += row[x].ToString();
                }
                fs.WriteLine(str);
            }
            fs.Close();
        }
    }
    private static void setDirDBs()
    {
        if (dirDBs == null) dirDBs = Environment.GetEnvironmentVariable(Decl.DIR_DBS);
        if (dirDBs == null)
            Console.WriteLine("�� ������ ���������� ����� " + Decl.DIR_DBS +
                ",\n\t\t\t   ������������ PATH DBs. ��� �� �����������:" +
                "\n\n\t���������-��������-��������������� ��������� �������-���������� �����");
    }
    /// <summary>
    /// WrReport(name,dt)   - ���������� ��������� ���� name � ������� �������
    /// </summary>
    /// <param name="name">string name - ��� ����� - ������ *.txt</param>
    /// <param name="dt">DataTable dt - ������� � ������� ��� ������</param>
    /// <journal>23.01.2015</journal>
    public static void WrReport(string name, DataTable dt)
    {
        setDirDBs();
        string fileName = dirDBs + @"\Reports\" + name + @".txt";
        using (StreamWriter fs = new StreamWriter(fileName, true, System.Text.Encoding.Default))
        {
            fs.WriteLine("--- " + DateTime.Now.ToLongTimeString() + " " + name + " ------------------");
            foreach (DataRow row in dt.Rows)
            {
                string str = "";
                foreach (DataColumn x in dt.Columns)
                {
                    if (str != "") str += '\t';
                    str += row[x].ToString();
                }
                fs.WriteLine(str);
            }
            fs.Close();
        }
    }
    private static void setDirDBs()
    {
        if (dirDBs == null) dirDBs = Environment.GetEnvironmentVariable(Decl.DIR_DBS);
        if (dirDBs == null)
            Console.WriteLine("�� ������ ���������� ����� " + Decl.DIR_DBS +
                ",\n\t\t\t   ������������ PATH DBs. ��� �� �����������:" +
                "\n\n\t���������-��������-��������������� ��������� �������-���������� �����");
    }
    public static void Quit() { _app.Quit(); }
    /// <summary>
    /// WrCSV(name) - ���������� CSV ���� ��� ��� ����������� ���� � SalesForce
    /// </summary>
    /// <param name="name">string name  - ��� ����� ��� ������</param>
    /// <journal>23/1/2015</journal>
    public static void WrCSV(string name, DataTable dt)
    {
        string pathCSV = @"C:/SFconstr/";    // �������, ���� ��������� CSV �����
        FileInfo file = new FileInfo(pathCSV + name + @".csv");
        StreamWriter fs = file.CreateText();

        foreach (DataRow row in dt.Rows)
        {
            string str = "";
            foreach (DataColumn x in dt.Columns)
            {
                if (str != "") str += ',';
                str += '"';
                str += row[x].ToString();
                str += '"';
            }
            fs.WriteLine(str); 
        }
        fs.Close();
    }
    public static long cellColorIndex(Excel.Worksheet Sh, int row, int col, string msg = "")
    {
        Log.set("cellColor");
        try
        {
            Excel.Range cell = Sh.Cells[row, col];
            return cell.Interior.ColorIndex;
        }
        catch
        {
            if (msg == null) return 0;
            if (msg == "") { msg = "Sheet[" + Sh.Name + "].Cell[" + row + "," + col + "]"; }
            Log.FATAL(msg);
            return 0;
        }
        finally { Log.exit(); }
    }
    /// <summary>
    /// isCellEmpty(sh,row,col)     - ���������� true, ���� ������ ����� sh[rw,col] ����� ��� ������ � ���������
    /// </summary>
    /// <param name="sh"></param>
    /// <param name="row"></param>
    /// <param name="col"></param>
    /// <returns></returns>
    /// <journal> 13.12.13 A.Pass
    /// </journal>
    public static bool isCellEmpty(Excel.Worksheet sh, int row, int col)
    {
        var value = sh.UsedRange.Cells[row, col].Value2;
        return (value == null || value.ToString().Trim() == "");
    }
#endif //end NOT_IN_USE