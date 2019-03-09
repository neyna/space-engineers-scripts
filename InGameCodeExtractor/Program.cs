using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace ConsoleApp2
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {

            string startString = "#region in-game";
            string line;
            string trimmed = "";
            bool read = false;
            int depth = 0;
            System.IO.StreamReader file;



            file = new System.IO.StreamReader("D:\\Dev\\space-engineers-scripts\\SpaceEngineers\\Extractor.cs"); //Open the currently active file


            while ((line = file.ReadLine()) != null)
            {
                if (!read && line.Contains(startString)) read = true;
                else if (read && line.Contains("#region")) depth++;
                else if (read && line.Contains("#endregion"))
                {
                    if (depth <= 0) break;
                    else depth--;
                }
                else if (read) trimmed += line.Trim() + "\n";
            }
            file.Close();

            //Clipboard.SetText(trimmed);
            if (trimmed!=null && trimmed != "")
            {
                Clipboard.SetText(trimmed);
            }            
        }
    }
}
