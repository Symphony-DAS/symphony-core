using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Repository;

namespace Symphony.Core
{
    /// <summary>
    /// Static utility class to manage logging within the Symphony.Core framework.
    /// </summary>
    public static class Logging
    {

        /// <summary>
        /// Configures basic logging to the console.
        /// <para>
        /// NB: Matlab does not currently support console output from .Net. You must log to a file
        /// if you are using Symphony.Core from Matlab.
        /// </para>
        /// </summary>
        public static void ConfigureConsole()
        {
            BasicConfigurator.Configure();
        }


        /// <summary>
        /// Configures loggin according to the given log4net configuration. Relative file
        /// paths of appenders specified in the config XML file will be created relative
        /// to logDirectory.
        /// </summary>
        /// <param name="configXmlFile">log4net XML configuration file</param>
        /// <param name="logDirectory">Directory root for relative file appender paths in the XML configuration file</param>
        public static void ConfigureLogging(FileInfo configXmlFile, string logDirectory)
        {
            XmlConfigurator.Configure(configXmlFile);
            ConfigureLogDirectory(logDirectory);
        }

        /// <summary>
        /// Configures loggin according to the given log4net configuration. Relative file
        /// paths of appenders specified in the config XML file will be created relative
        /// to logDirectory.
        /// </summary>
        /// <param name="configXmlFilePath">log4net XML configuration file path</param>
        /// <param name="logDirectory">Directory root for relative file appender paths in the XML configuration file</param>
        public static void ConfigureLogging(string configXmlFilePath, string logDirectory)
        {
            XmlConfigurator.Configure(new FileInfo(configXmlFilePath));

            //get the current logging repository for this application 
            ConfigureLogDirectory(logDirectory);
        }

        private static void ConfigureLogDirectory(string logDirectory)
        {
            ILoggerRepository repository = LogManager.GetRepository();
            //get all of the appenders for the repository 
            IAppender[] appenders = repository.GetAppenders();
            //only change the file path on the 'FileAppenders' 
            foreach (IAppender appender in (from iAppender in appenders
                                            where iAppender is FileAppender
                                            select iAppender))
            {
                var fileAppender = appender as FileAppender;
                //set the path to your logDirectory using the original file name defined 
                //in configuration 
                if (fileAppender != null)
                {
                    fileAppender.File = Path.Combine(logDirectory, Path.GetFileName(fileAppender.File));
                    //make sure to call fileAppender.ActivateOptions() to notify the logging 
                    //sub system that the configuration for this appender has changed. 
                    fileAppender.ActivateOptions();
                }
            }
        }
    }
}
