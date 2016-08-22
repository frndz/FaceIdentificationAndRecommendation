using System;
using System.Windows.Forms;

namespace MultiFaceRec
{
    static class Program
    {
        /// <summary>
        /// Starting point of application
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FrmPrincipal());
        }
    }
}
