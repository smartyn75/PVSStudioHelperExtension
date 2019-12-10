using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PVSStudioHelper.Implementation
{
    class Report
    {
        public string SolutionName { get; internal set; }
        public int ProcessedItems { get; set; }
        public int ProcessedOpenedItems { get; set; }
    }
}
