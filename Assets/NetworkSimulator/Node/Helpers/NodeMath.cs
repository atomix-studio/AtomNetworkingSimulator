using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.Helpers
{
    public static class NodeMath
    {
        public static float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax)
        {
            float deltaIn = inputMax - inputMin;
            float deltaOut = outputMax - outputMin;

            float ratio = (value - inputMin) / deltaIn;
            return ratio * deltaOut + outputMin;
        }
    }
}
