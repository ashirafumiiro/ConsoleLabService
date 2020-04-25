using System.Collections.Generic;
using System.Web.Http;
using System.Windows.Forms;
using System.Linq;
using System;

namespace LabServiceLibrary
{
    public class TimeController : ApiController
    {

        // GET api/time
        public string Get()
        {
            DateTime time = DateTime.Now;
            return time.ToString("MM/dd/yyyy hh:mm tt");
        }
    }
}