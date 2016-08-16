using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Symphony.Core;

namespace NI
{
    class NIDAQInputStream : DAQInputStream
    {
        public NIDAQInputStream(string name, NIDAQController controller)
            : base(name, controller)
        {
            Clock = controller.Clock;
        }
    }
}
