using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionMatchWin10
{
    public class Motion
    {
        public string id = "";
        public string User = "";
        public string ActivityName = "";
        public DateTime TimeStamp;
        public string TrackedBodyPart = "";
        //public List<Sample> Samples;
        public string Quality;

        public Motion()
        {
            //Samples = new List<Sample>();
            TimeStamp = DateTime.Now;
        }
    }
}
