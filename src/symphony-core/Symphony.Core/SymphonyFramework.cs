using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Symphony.Core
{
    /// <summary>
    /// Information about the framework.
    /// </summary>
    public class SymphonyFramework
    {
        /// <summary>
        /// Symphony.Core version string
        /// </summary>
        public static String VersionString
        {
            get { return Version.ToString(); }
        }

        /// <summary>
        /// Version of the Symphony.Core framework
        /// </summary>
        public static Version Version
        {
            get { return new Version("0.9.1"); }
        }
    }
}
