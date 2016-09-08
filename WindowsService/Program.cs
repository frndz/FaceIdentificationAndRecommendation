using System.ServiceProcess;

namespace WindowsServiceCS
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            /*if (Environment.UserInteractive)
            {
                string parameter = string.Concat(args);
                switch (parameter)
                {
                    case "--install":
                    ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                    break;
                    case "--uninstall":
                    ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                    break;
                }
            }
            else
            {
                ServiceBase[] servicesToRun = new ServiceBase[]{ 
                                      new Service1() 
                                  };
                ServiceBase.Run(servicesToRun);
            }*/
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new Service1() 
			};
            ServiceBase.Run(ServicesToRun);
        }
    }
}
